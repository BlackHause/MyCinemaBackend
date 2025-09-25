using System;
using System.Security.Cryptography;
using System.Text;

public static class Md5Crypt
{
    private const string Itoa64 = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    private static string To64(long v, int n)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            sb.Append(Itoa64[(int)(v & 0x3f)]);
            v >>= 6;
        }
        return sb.ToString();
    }

    public static string Crypt(string password, string salt)
    {
        const string magic = "$1$";
        if (salt.StartsWith(magic))
        {
            salt = salt.Substring(magic.Length);
        }
        salt = salt.Split('$')[0];
        salt = salt.Length > 8 ? salt.Substring(0, 8) : salt;

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] saltBytes = Encoding.UTF8.GetBytes(salt);
        byte[] magicBytes = Encoding.UTF8.GetBytes(magic);

        using (var md5 = MD5.Create())
        {
            var ctx = new MemoryStream();
            ctx.Write(passwordBytes, 0, passwordBytes.Length);
            ctx.Write(magicBytes, 0, magicBytes.Length);
            ctx.Write(saltBytes, 0, saltBytes.Length);

            byte[] final = md5.ComputeHash(new MemoryStream(passwordBytes.Concat(saltBytes).Concat(passwordBytes).ToArray()));

            for (int pl = password.Length; pl > 0; pl -= 16)
            {
                ctx.Write(final, 0, pl > 16 ? 16 : pl);
            }

            for (int i = password.Length; i > 0; i >>= 1)
            {
                ctx.Write((i & 1) == 1 ? new byte[] { 0 } : new[] { (byte)password[0] }, 0, 1);
            }
            
            final = md5.ComputeHash(ctx.ToArray());

            for (int i = 0; i < 1000; i++)
            {
                var ctx1 = new MemoryStream();
                if ((i & 1) != 0) ctx1.Write(passwordBytes, 0, passwordBytes.Length);
                else ctx1.Write(final, 0, final.Length);

                if (i % 3 != 0) ctx1.Write(saltBytes, 0, saltBytes.Length);
                if (i % 7 != 0) ctx1.Write(passwordBytes, 0, passwordBytes.Length);
                
                if ((i & 1) != 0) ctx1.Write(final, 0, final.Length);
                else ctx1.Write(passwordBytes, 0, passwordBytes.Length);

                final = md5.ComputeHash(ctx1.ToArray());
            }

            var sb = new StringBuilder();
            sb.Append(To64((final[0] << 16) | (final[6] << 8) | final[12], 4));
            sb.Append(To64((final[1] << 16) | (final[7] << 8) | final[13], 4));
            sb.Append(To64((final[2] << 16) | (final[8] << 8) | final[14], 4));
            sb.Append(To64((final[3] << 16) | (final[9] << 8) | final[15], 4));
            sb.Append(To64((final[4] << 16) | (final[10] << 8) | final[5], 4));
            sb.Append(To64(final[11], 2));

            return $"{magic}{salt}${sb.ToString()}";
        }
    }
}