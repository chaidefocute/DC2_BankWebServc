using DataWebAPI.Models;
using Microsoft.Data.Sqlite;

namespace DataWebAPI.Services
{
    public class UserService : IUserService
    {
        private readonly string _connStr;

        private readonly string _root;

        // columns read by ReadUser (same order)
        private const string UserColumns = "UserId, Username, Name, Email, Address, Phone, PicturePath, Role, IsActive";

        public UserService(IConfiguration config, IWebHostEnvironment env)
        {
            _connStr = config.GetConnectionString("DefaultConnection") ?? "Data Source=bank.db";
            _root = env.ContentRootPath;
        }

        private SqliteConnection Open()
        {
            var conn = new SqliteConnection(_connStr);
            conn.Open();
            return conn;
        }

        // values are sent as parameters, never joined into the sql (stops sql injection)
        private static SqliteCommand Cmd(SqliteConnection conn, string sql, params (string Name, object? Value)[] ps)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in ps)
                cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return cmd;
        }

        private static UserDto ReadUser(SqliteDataReader rd) => new()
        {
            UserId = rd.GetInt32(0),
            Username = rd.GetString(1),
            Name = rd.GetString(2),
            Email = rd.GetString(3),
            Address = rd.IsDBNull(4) ? null : rd.GetString(4),
            Phone = rd.IsDBNull(5) ? null : rd.GetString(5),
            PicturePath = rd.IsDBNull(6) ? null : rd.GetString(6),
            Role = rd.GetString(7),
            IsActive = rd.GetInt32(8) != 0
        };

        // the db has UNIQUE on Username and Email, sqlite error 2067 = unique constraint failed
        private static ConflictException Duplicate(SqliteException ex)
        {
            var field = ex.Message.Contains("Users.Email") ? "Email" : "Username";
            return new ConflictException($"{field} is already taken.");
        }

        public Task<UserDto> CreateUserAsync(CreateUserRequest r)
        {
            using var conn = Open();
            int id;
            try
            {
                // only the hash is saved, PasswordHasher puts the salt inside the hash string
                using var cmd = Cmd(conn,
                    @"INSERT INTO Users (Username, Name, Email, Address, Phone, PasswordHash, Role, IsActive)
                      VALUES (@u, @n, @e, @a, @p, @h, @r, 1);
                      SELECT last_insert_rowid();",
                    ("@u", r.Username), ("@n", r.Name), ("@e", r.Email), ("@a", r.Address), ("@p", r.Phone),
                    ("@h", PasswordHasher.Hash(r.Password)), ("@r", r.Role));
                id = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067)
            {
                throw Duplicate(ex);
            }

            return GetUserAsync(id);
        }

        public Task<UserDto> GetUserAsync(int userId) => FindUser("UserId", userId);
        public Task<UserDto> GetUserByUsernameAsync(string username) => FindUser("Username", username);
        public Task<UserDto> GetUserByEmailAsync(string email) => FindUser("Email", email);

        // column is always one of the fixed names above, never user input
        private Task<UserDto> FindUser(string column, object value)
        {
            using var conn = Open();
            using var cmd = Cmd(conn, $"SELECT {UserColumns} FROM Users WHERE {column} = @v", ("@v", value));
            using var rd = cmd.ExecuteReader();

            if (!rd.Read())
                throw new NotFoundException($"User '{value}' not found.");

            return Task.FromResult(ReadUser(rd));
        }

        public Task UpdateUserAsync(int userId, UpdateUserRequest r)
        {
            using var conn = Open();
            try
            {
                using var cmd = Cmd(conn, "UPDATE Users SET Name = @n, Email = @e, Address = @a, Phone = @p WHERE UserId = @id",
                    ("@n", r.Name), ("@e", r.Email), ("@a", r.Address), ("@p", r.Phone), ("@id", userId));

                // 0 rows changed = user does not exist
                if (cmd.ExecuteNonQuery() == 0)
                    throw new NotFoundException($"User {userId} not found.");
            }
            catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == 2067)
            {
                throw Duplicate(ex);
            }

            return Task.CompletedTask;
        }

        // soft delete, the row stays because accounts point to it
        public Task DeactivateUserAsync(int userId)
        {
            using var conn = Open();
            using var cmd = Cmd(conn, "UPDATE Users SET IsActive = 0 WHERE UserId = @id", ("@id", userId));

            if (cmd.ExecuteNonQuery() == 0)
                throw new NotFoundException($"User {userId} not found.");

            return Task.CompletedTask;
        }

        public Task<PagedResult<UserDto>> SearchUsersAsync(UserQuery q)
        {
            using var conn = Open();

            var where = new List<string>();
            var ps = new List<(string Name, object? Value)>();

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                // also finds the owner of an account number (subquery on the Accounts table)
                where.Add("(Username LIKE @s OR Name LIKE @s OR Email LIKE @s OR UserId IN (SELECT UserId FROM Accounts WHERE AccountNumber LIKE @s))");
                ps.Add(("@s", "%" + q.Search + "%"));
            }
            if (!string.IsNullOrWhiteSpace(q.Role))
            {
                where.Add("Role = @r");
                ps.Add(("@r", q.Role));
            }
            string whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

            int total;
            using (var cnt = Cmd(conn, $"SELECT COUNT(*) FROM Users {whereSql}", ps.ToArray()))
                total = Convert.ToInt32(cnt.ExecuteScalar());

            ps.Add(("@size", q.PageSize));
            ps.Add(("@off", (q.Page - 1) * q.PageSize));

            var items = new List<UserDto>();
            using (var cmd = Cmd(conn, $"SELECT {UserColumns} FROM Users {whereSql} ORDER BY UserId LIMIT @size OFFSET @off", ps.ToArray()))
            using (var rd = cmd.ExecuteReader())
            {
                while (rd.Read())
                    items.Add(ReadUser(rd));
            }

            return Task.FromResult(new PagedResult<UserDto>
            {
                Items = items,
                Page = q.Page,
                PageSize = q.PageSize,
                TotalCount = total,
                TotalPages = (int)Math.Ceiling(total / (double)q.PageSize)
            });
        }

        public Task ResetPasswordAsync(int userId, ResetPasswordRequest r)
        {
            using var conn = Open();
            using var cmd = Cmd(conn, "UPDATE Users SET PasswordHash = @h WHERE UserId = @id",
                ("@h", PasswordHasher.Hash(r.NewPassword)), ("@id", userId));

            if (cmd.ExecuteNonQuery() == 0)
                throw new NotFoundException($"User {userId} not found.");

            return Task.CompletedTask;
        }

        // returns null if the username or password is wrong
        public Task<UserDto?> LoginAsync(LoginRequest r)
        {
            using var conn = Open();
            using var cmd = Cmd(conn, $"SELECT {UserColumns}, PasswordHash FROM Users WHERE Username = @u", ("@u", r.Username));
            using var rd = cmd.ExecuteReader();

            // same result for unknown username and wrong password, so nobody can guess which usernames exist
            if (!rd.Read() || !PasswordHasher.Verify(r.Password, rd.GetString(9)))
                return Task.FromResult<UserDto?>(null);

            var user = ReadUser(rd);
            if (!user.IsActive)
                throw new BusinessRuleException("USER_INACTIVE", "This user has been deactivated.");

            return Task.FromResult<UserDto?>(user);
        }

        // the picture is saved as a file, only its path goes in the db
        public async Task<UserDto> SavePictureAsync(int userId, IFormFile file)
        {
            var user = await GetUserAsync(userId);

            // saved as App_Data/pictures/{userId}.jpg (or .png), a new upload replaces the old one
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var path = $"App_Data/pictures/{userId}{ext}";
            var fullPath = Path.Combine(_root, path);

            if (user.PicturePath != null && user.PicturePath != path)
                File.Delete(Path.Combine(_root, user.PicturePath));

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using (var fs = File.Create(fullPath))
                await file.CopyToAsync(fs);

            using var conn = Open();
            using var cmd = Cmd(conn, "UPDATE Users SET PicturePath = @pic WHERE UserId = @id", ("@pic", path), ("@id", userId));
            cmd.ExecuteNonQuery();

            user.PicturePath = path;
            return user;
        }

        public async Task<string> GetPictureFileAsync(int userId)
        {
            var user = await GetUserAsync(userId);
            var fullPath = user.PicturePath == null ? null : Path.Combine(_root, user.PicturePath);

            if (fullPath == null || !File.Exists(fullPath))
                throw new NotFoundException($"User {userId} has no profile picture.");

            return fullPath;
        }
    }
}
