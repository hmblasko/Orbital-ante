using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OrbitalAnte.Core
{
    /// <summary>Persistent identities for code-built control slots, independent of binding order and defaults.</summary>
    public static class BindingIdentity
    {
        // This namespace and encoding are a save contract. Renaming a map, action or named slot needs a migration.
        public static Guid Create(string map, string action, string groups, string name, string kind, string composite = null)
        {
            if (string.IsNullOrEmpty(map)) throw new ArgumentException("A map name is required.", nameof(map));
            if (string.IsNullOrEmpty(action)) throw new ArgumentException("An action name is required.", nameof(action));
            if (string.IsNullOrEmpty(kind)) throw new ArgumentException("A binding kind is required.", nameof(kind));

            string key = "OrbitalAnte.Binding.v1" + Field(map) + Field(action) + Field(groups) + Field(name) + Field(kind) + Field(composite);
            using (var hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(key));
                var bytes = new byte[16];
                Array.Copy(digest, bytes, bytes.Length);
                return new Guid(bytes);
            }
        }

        static string Field(string value)
        {
            value = value ?? string.Empty;
            return value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
        }
    }
}
