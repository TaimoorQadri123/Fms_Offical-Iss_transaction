using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FMSMyService.Helpers
{
    public class Decryption
    {
        private const string key = "";

        public string DecryptString(string ciphertext)
        {
            byte[] iv = new byte[16];
            byte [] buffer = Convert.FromBase64String(ciphertext);
            using (Aes aes = Aes.Create())
            {
                aes.Key= Encoding.UTF8 .GetBytes(key);
                aes.IV = iv;
                ICryptoTransform decrptor = aes.CreateDecryptor(aes.Key,aes.IV);
                using (MemoryStream memoryStream = new MemoryStream(buffer))
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decrptor, CryptoStreamMode.Read))
                using (StreamReader streamreader = new StreamReader(cryptoStream))
                {
                    return streamreader.ReadToEnd();
                }


            }
        }
        

    }
}
