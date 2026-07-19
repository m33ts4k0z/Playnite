#if !WINDOWS
using Microsoft.Data.Sqlite;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Playnite.Common
{
    public class Sqlite : ISQLite, IDisposable
    {
        private SqliteConnection connection;

        public Sqlite(string dbPath, SqliteOpenFlags openFlags)
        {
            var builder = CreateConnectionString(dbPath, openFlags);
            connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
        }

        private static SqliteConnectionStringBuilder CreateConnectionString(string dbPath, SqliteOpenFlags openFlags)
        {
            const SqliteOpenFlags mutexFlags = SqliteOpenFlags.NoMutex | SqliteOpenFlags.FullMutex;
            if ((openFlags & mutexFlags) != 0)
            {
                throw new NotSupportedException("The portable SQLite provider does not expose per-connection mutex flags.");
            }

            const SqliteOpenFlags protectionFlags = SqliteOpenFlags.ProtectionComplete |
                SqliteOpenFlags.ProtectionCompleteUnlessOpen |
                SqliteOpenFlags.ProtectionCompleteUntilFirstUserAuthentication |
                SqliteOpenFlags.ProtectionNone;
            if ((openFlags & protectionFlags) != 0)
            {
                throw new PlatformNotSupportedException("Apple file-protection SQLite flags are not supported by this provider.");
            }

            if (openFlags.HasFlag(SqliteOpenFlags.ReadOnly) && openFlags.HasFlag(SqliteOpenFlags.ReadWrite))
            {
                throw new ArgumentException("SQLite cannot be opened as both read-only and read-write.", nameof(openFlags));
            }

            if (openFlags.HasFlag(SqliteOpenFlags.ReadOnly) && openFlags.HasFlag(SqliteOpenFlags.Create))
            {
                throw new ArgumentException("A read-only SQLite connection cannot create a database.", nameof(openFlags));
            }

            if (openFlags.HasFlag(SqliteOpenFlags.SharedCache) && openFlags.HasFlag(SqliteOpenFlags.PrivateCache))
            {
                throw new ArgumentException("SQLite cannot use both shared and private cache modes.", nameof(openFlags));
            }

            const SqliteOpenFlags knownFlags = SqliteOpenFlags.ReadOnly | SqliteOpenFlags.ReadWrite | SqliteOpenFlags.Create |
                SqliteOpenFlags.NoMutex | SqliteOpenFlags.FullMutex | SqliteOpenFlags.SharedCache | SqliteOpenFlags.PrivateCache |
                protectionFlags;
            if ((openFlags & ~knownFlags) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(openFlags), "Unknown SQLite open flags were specified.");
            }

            var mode = openFlags.HasFlag(SqliteOpenFlags.ReadOnly)
                ? SqliteOpenMode.ReadOnly
                : openFlags.HasFlag(SqliteOpenFlags.Create)
                    ? SqliteOpenMode.ReadWriteCreate
                    : SqliteOpenMode.ReadWrite;
            var cache = openFlags.HasFlag(SqliteOpenFlags.SharedCache)
                ? SqliteCacheMode.Shared
                : SqliteCacheMode.Private;
            return new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = mode,
                Cache = cache,
                Pooling = false
            };
        }

        public List<T> Query<T>(string query, params object[] args) where T : new()
        {
            ObjectDisposedException.ThrowIf(connection == null, this);
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("A SQLite query is required.", nameof(query));
            }

            using (var command = connection.CreateCommand())
            {
                var arguments = args ?? Array.Empty<object>();
                command.CommandText = BindPositionalArguments(query, arguments);
                for (var index = 0; index < arguments.Length; index++)
                {
                    command.Parameters.AddWithValue("$p" + index.ToString(CultureInfo.InvariantCulture), arguments[index] ?? DBNull.Value);
                }

                using (var reader = command.ExecuteReader())
                {
                    var result = new List<T>();
                    while (reader.Read())
                    {
                        var item = new T();
                        for (var column = 0; column < reader.FieldCount; column++)
                        {
                            SetMemberValue(item, reader.GetName(column), reader.IsDBNull(column) ? null : reader.GetValue(column));
                        }

                        result.Add(item);
                    }

                    return result;
                }
            }
        }

        private static string BindPositionalArguments(string query, object[] args)
        {
            var bound = query;
            for (var index = 0; index < args.Length; index++)
            {
                var parameter = "$p" + index.ToString(CultureInfo.InvariantCulture);
                bound = bound.Replace("{" + index.ToString(CultureInfo.InvariantCulture) + "}", parameter, StringComparison.Ordinal);
                var anonymous = bound.IndexOf('?', StringComparison.Ordinal);
                if (anonymous >= 0)
                {
                    bound = bound.Substring(0, anonymous) + parameter + bound.Substring(anonymous + 1);
                }
            }

            return bound;
        }

        private static void SetMemberValue<T>(T item, string memberName, object value)
        {
            var type = typeof(T);
            var property = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate => candidate.CanWrite && candidate.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase));
            if (property != null)
            {
                property.SetValue(item, ConvertValue(value, property.PropertyType));
                return;
            }

            var field = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(candidate => candidate.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase));
            if (field != null)
            {
                field.SetValue(item, ConvertValue(value, field.FieldType));
            }
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            var convertedType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (convertedType.IsInstanceOfType(value))
            {
                return value;
            }

            if (convertedType == typeof(Guid))
            {
                return value is byte[] bytes ? new Guid(bytes) : Guid.Parse(value.ToString());
            }

            if (convertedType.IsEnum)
            {
                return value is string text
                    ? Enum.Parse(convertedType, text, true)
                    : Enum.ToObject(convertedType, value);
            }

            return Convert.ChangeType(value, convertedType, CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            connection?.Dispose();
            connection = null;
        }
    }
}
#endif
