using System.Net;
using System.Security.Claims;
using System.Text.Json;
using BankMvc.Models;
using RestSharp;

namespace BankMvc.Services
{
    // the ONLY place that knows Part A's URLs
    public static class Routes
    {
        // all accounts of one user, the user ID is sent in the query (Part A: GetAccountsByUserAsync)
        public const string Accounts = "api/accounts";

        // all transactions with paging, sorting and filters (Part A: SearchTransactionsAsync)
        public const string Transactions = "api/transactions";

        // transfer money between 2 accounts (Part A: TransferAsync)
        public const string Transfer = "api/transactions/transfer";

        // Records an action and provides a list with filters and paging
        public const string Audit = "api/audit";

        // Checks if the service and database is up
        public const string Health = "api/health";

        // create a user (Part A: CreateUserAsync)
        public const string Users = "api/users";

        // check a username + password, gives back the user with their role (Part A: LoginAsync)
        public const string Login = "api/users/login";
    }

    // object to hold the result of every call to the service (success or error)
    // so controllers never have to deal with exceptions
    public class ApiResult<T>
    {
        // true if the service replied with success
        public bool Ok { get; set; }

        // the data from the service (only when Ok)
        public T? Data { get; set; }

        // friendly message to show the user (only when not Ok)
        public string Message { get; set; } = "";

        // messages for each field from the service's "details" (validation errors)
        public List<string> Details { get; set; } = new();
    }

    public class BankApiClient
    {
        // ignore the upper/lower case of the json names when reading the reply
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        // the client used to send all the requests to the service
        private readonly RestClient _client;

        public BankApiClient(IConfiguration config)
        {
            // base URL comes from appsettings.json, NOT hardcoded
            var baseUrl = config["BankService:BaseUrl"]
                ?? throw new InvalidOperationException("BankService:BaseUrl is missing in appsettings.json");

            // 10 second timeout so the page doesn't hang forever if the service is stuck
            _client = new RestClient(new RestClientOptions(baseUrl) { Timeout = TimeSpan.FromSeconds(10) });
        }

        // get all accounts of a user (and also used to check who owns what)
        public Task<ApiResult<List<AccountDto>>> GetAccountsAsync(int userId) =>
            Send<List<AccountDto>>(new RestRequest(Routes.Accounts).AddQueryParameter("userId", userId.ToString()));

        // get transactions with the filters, paging and sorting
        public Task<ApiResult<PagedResult<TransactionDto>>> GetTransactionsAsync(TransactionQuery q)
        {
            var req = new RestRequest(Routes.Transactions);

            // only send the filters that were filled in
            if (!string.IsNullOrWhiteSpace(q.AccountNumber)) req.AddQueryParameter("accountNumber", q.AccountNumber);
            if (!string.IsNullOrWhiteSpace(q.Search)) req.AddQueryParameter("search", q.Search);
            if (q.From.HasValue) req.AddQueryParameter("from", q.From.Value.ToString("yyyy-MM-dd"));
            if (q.To.HasValue) req.AddQueryParameter("to", q.To.Value.ToString("yyyy-MM-dd"));

            // sorting and paging are always sent
            req.AddQueryParameter("sortBy", q.SortBy);
            req.AddQueryParameter("sortDir", q.SortDir);
            req.AddQueryParameter("page", q.Page.ToString());
            req.AddQueryParameter("pageSize", q.PageSize.ToString());

            return Send<PagedResult<TransactionDto>>(req);
        }

        // transfer money, service gives back the sender's transaction
        public Task<ApiResult<TransactionDto>> TransferAsync(TransferViewModel m)
        {
            var req = new RestRequest(Routes.Transfer, Method.Post)
                .AddJsonBody(new { m.FromAccountNumber, m.ToAccountNumber, m.Amount, m.Description });

            return Send<TransactionDto>(req);
        }

        // ---------- users ----------

        // get one user's details
        public Task<ApiResult<UserDto>> GetUserAsync(int userId) =>
            Send<UserDto>(new RestRequest($"{Routes.Users}/{userId}"));

        // update a user's name, email, address and phone (the service replies 204 with no body)
        public Task<ApiResult<JsonElement>> UpdateUserAsync(int userId, ProfileViewModel m) =>
            Send<JsonElement>(new RestRequest($"{Routes.Users}/{userId}", Method.Put)
                .AddJsonBody(new { m.Name, m.Email, m.Address, m.Phone }));

        // upload a profile picture, the service checks the file type and size
        public Task<ApiResult<UserDto>> UploadPictureAsync(int userId, IFormFile file)
        {
            var req = new RestRequest($"{Routes.Users}/{userId}/picture", Method.Post);
            req.AddFile("file", () => file.OpenReadStream(), file.FileName, file.ContentType);
            return Send<UserDto>(req);
        }

        // list / search users with paging (admin users page)
        public Task<ApiResult<PagedResult<UserDto>>> SearchUsersAsync(UserSearchQuery q)
        {
            var req = new RestRequest(Routes.Users);
            if (!string.IsNullOrWhiteSpace(q.Search)) req.AddQueryParameter("search", q.Search);
            if (!string.IsNullOrWhiteSpace(q.Role)) req.AddQueryParameter("role", q.Role);
            req.AddQueryParameter("page", q.Page.ToString());
            req.AddQueryParameter("pageSize", q.PageSize.ToString());
            return Send<PagedResult<UserDto>>(req);
        }

        // deactivate a user (the service replies 204 with no body)
        public Task<ApiResult<JsonElement>> DeactivateUserAsync(int userId) =>
            Send<JsonElement>(new RestRequest($"{Routes.Users}/{userId}", Method.Delete));

        // set a new password for a user (the service replies 204 with no body)
        public Task<ApiResult<JsonElement>> ResetPasswordAsync(int userId, string newPassword) =>
            Send<JsonElement>(new RestRequest($"{Routes.Users}/{userId}/password", Method.Put)
                .AddJsonBody(new { newPassword }));

        // get the picture file, null if the user has none or the service is down
        public async Task<(byte[] Data, string ContentType)?> GetPictureAsync(int userId)
        {
            try
            {
                var resp = await _client.ExecuteAsync(new RestRequest($"{Routes.Users}/{userId}/picture"));
                if (!resp.IsSuccessful || resp.RawBytes == null)
                    return null;
                return (resp.RawBytes, resp.ContentType ?? "image/jpeg");
            }
            catch
            {
                return null;
            }
        }

        // ---------- sign in / register ----------

        // check the username + password, wrong details come back as Ok = false with the service's message
        public Task<ApiResult<UserDto>> LoginAsync(string username, string password) =>
            Send<UserDto>(new RestRequest(Routes.Login, Method.Post).AddJsonBody(new { username, password }));

        // create a new customer, the role is always Customer so nobody can register themselves as an admin
        public Task<ApiResult<UserDto>> RegisterAsync(RegisterViewModel m) =>
            Send<UserDto>(new RestRequest(Routes.Users, Method.Post)
                .AddJsonBody(new { m.Username, m.Name, m.Email, m.Password, m.Address, m.Phone, role = "Customer" }));

        // create a user with the role the admin picked (admin create user page)
        public Task<ApiResult<UserDto>> CreateUserAsync(RegisterViewModel m, string role) =>
            Send<UserDto>(new RestRequest(Routes.Users, Method.Post)
                .AddJsonBody(new { m.Username, m.Name, m.Email, m.Password, m.Address, m.Phone, role }));

        // ---------- audit trail ----------

        // record an admin action, ex: LogAdminActionAsync(User, "RESET_PASSWORD", "User", "7", "Password reset by admin")
        // takes the admin's ID from the login cookie so callers can't fake it
        // an audit failure never stops the real action, the caller can just ignore the result
        public Task<ApiResult<AuditEntryDto>> LogAdminActionAsync(ClaimsPrincipal admin, string action,
            string? targetType = null, string? targetId = null, string? details = null)
        {
            if (!int.TryParse(admin.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
                return Task.FromResult(Fail<AuditEntryDto>("Not signed in as an administrator."));

            var req = new RestRequest(Routes.Audit, Method.Post)
                .AddJsonBody(new { adminUserId = adminId, action, targetType, targetId, details });
            return Send<AuditEntryDto>(req);
        }

        // get audit entries for the admin dashboard
        public Task<ApiResult<PagedResult<AuditEntryDto>>> GetAuditLogAsync(AuditQuery q)
        {
            var req = new RestRequest(Routes.Audit);
            if (q.AdminUserId.HasValue) req.AddQueryParameter("adminUserId", q.AdminUserId.Value.ToString());
            if (!string.IsNullOrWhiteSpace(q.Search)) req.AddQueryParameter("search", q.Search);
            if (!string.IsNullOrWhiteSpace(q.ActionType)) req.AddQueryParameter("action", q.ActionType);
            if (q.From.HasValue) req.AddQueryParameter("from", q.From.Value.ToString("yyyy-MM-dd"));
            if (q.To.HasValue) req.AddQueryParameter("to", q.To.Value.ToString("yyyy-MM-dd"));
            req.AddQueryParameter("sortDir", q.SortDir);
            req.AddQueryParameter("page", q.Page.ToString());
            req.AddQueryParameter("pageSize", q.PageSize.ToString());
            return Send<PagedResult<AuditEntryDto>>(req);
        }

        // ---------- resilience ----------

        // true if the service answers /api/health, used to show a warning banner on the home page
        public async Task<bool> IsServiceUpAsync()
        {
            var result = await Send<JsonElement>(new RestRequest(Routes.Health));
            return result.Ok;
        }

        // helper method to send the request, it always gives back an ApiResult (it never throws)
        private async Task<ApiResult<T>> Send<T>(RestRequest req)
        {
            RestResponse resp;
            try
            {
                resp = await _client.ExecuteAsync(req);
            }
            catch
            {
                // the request failed before we got any reply from the service
                return Fail<T>("The bank service is unavailable. Please try again later.");
            }

            // status 0 = no answer at all (service is down), 503 = service unavailable
            if (resp.StatusCode == 0 || resp.StatusCode == HttpStatusCode.ServiceUnavailable)
                return Fail<T>("The bank service is unavailable. Please try again later.");

            // if successful, read the json into the type we asked for
            if (resp.IsSuccessful)
            {
                try
                {
                    var data = string.IsNullOrWhiteSpace(resp.Content)
                        ? default
                        : JsonSerializer.Deserialize<T>(resp.Content, Json);
                    return new ApiResult<T> { Ok = true, Data = data };
                }
                catch
                {
                    return Fail<T>("The bank service sent a response we couldn't read.");
                }
            }

            // if error, part A sends { error, message, details } so we take the message from it
            var result = Fail<T>("Something went wrong. Please try again.");
            try
            {
                using var doc = JsonDocument.Parse(resp.Content ?? "{}");

                // the main message, ex: "Balance is 45.20; requested 100.00."
                if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    result.Message = msg.GetString()!;

                // details = messages for each field (only for validation errors, otherwise null)
                if (doc.RootElement.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in details.EnumerateObject())
                    {
                        if (field.Value.ValueKind == JsonValueKind.Array)
                            foreach (var item in field.Value.EnumerateArray())
                                result.Details.Add(item.ToString());
                        else
                            result.Details.Add(field.Value.ToString());
                    }
                }
            }
            catch
            {
                // the reply is not json, so keep the default message (don't show raw errors)
            }
            return result;
        }

        // helper method to create a failed result with a message
        private static ApiResult<T> Fail<T>(string message) => new() { Ok = false, Message = message };
    }
}
