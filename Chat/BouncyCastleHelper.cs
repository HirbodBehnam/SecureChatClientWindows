using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Chat
{
    public static class BouncyCastleHelper
    {
        /// <summary>
        /// Decrypt a message with RSA Oaep SHA 512
        /// </summary>
        /// <param name="key">The RSA private keys</param>
        /// <param name="data">Data to decrypt</param>
        /// <returns>The decrypted data</returns>
        public static byte[] RsaDecrypt(AsymmetricKeyParameter key, byte[] data)
        {
            var encryptEngine = new OaepEncoding(new RsaEngine(), new Sha512Digest());
            encryptEngine.Init(false,key);
            return encryptEngine.ProcessBlock(data, 0, data.Length);
        }
        /// <summary>
        /// Encrypts the data with RSA oaep SHA 512
        /// </summary>
        /// <param name="key">The RSA public keys</param>
        /// <param name="data">Data to encrypt</param>
        /// <returns>Encrypted data</returns>
        public static byte[] RsaEncrypt(AsymmetricKeyParameter key, string data) => RsaEncrypt(key, Encoding.UTF8.GetBytes(data));

        /// <summary>
        /// Encrypts the data with RSA oaep SHA 512
        /// </summary>
        /// <param name="key">The RSA public keys</param>
        /// <param name="data">Data to encrypt</param>
        /// <returns>Encrypted data</returns>
        public static byte[] RsaEncrypt(AsymmetricKeyParameter key, byte[] data)
        {
            var encryptEngine = new OaepEncoding(new RsaEngine(), new Sha512Digest());
            encryptEngine.Init(true,key);
            return encryptEngine.ProcessBlock(data, 0, data.Length);
        }
        /// <summary>
        /// Convert RSA pem files to <see cref="AsymmetricKeyParameter"/>
        /// </summary>
        /// <param name="pemFile">The content of the file. (NOT THE FILE NAME)</param>
        /// <returns>The key to use in bouncy castle</returns>
        public static AsymmetricKeyParameter ReadAsymmetricKeyParameter(string pemFile)
        {
            using (StringReader reader = new StringReader(pemFile))
            {
                Org.BouncyCastle.OpenSsl.PemReader pr =
                    new Org.BouncyCastle.OpenSsl.PemReader(reader);
                Org.BouncyCastle.Utilities.IO.Pem.PemObject po = pr.ReadPemObject();
 
                return PublicKeyFactory.CreateKey(po.Content);
            }
        }
        /// <summary>
        /// Decrypt a message with AES-GCM cipher; The nonce is first 12 bytes of payload
        /// </summary>
        /// <param name="payload">The message to decrypt</param>
        /// <param name="key">The key to decrypt it with it</param>
        /// <returns>Decrypted message with UTF8 encoded</returns>
        public static string AesGcmDecrypt(string payload, string key) =>
            Encoding.UTF8.GetString(AesGcmDecrypt(Convert.FromBase64String(payload),
                Convert.FromBase64String(key)));

        /// <summary>
        /// Decrypt a message with AES-GCM cipher; The nonce is first 12 bytes of payload
        /// </summary>
        /// <param name="payload">The message to decrypt</param>
        /// <param name="key">The key to decrypt it with it</param>
        /// <returns>Decrypted message</returns>
        public static byte[] AesGcmDecrypt(byte[] payload, byte[] key)
        {
            byte[] realPayload = new byte[payload.Length - 12],nonce = new byte[12];
            Buffer.BlockCopy(payload, 0, nonce, 0, 12); // get the first 12 bytes as nonce
            Buffer.BlockCopy(payload, 12, realPayload, 0, payload.Length - 12); // get the rest as the payload
            return AesGcmDecrypt(realPayload, key, nonce);
        }
        /// <summary>
        /// Decrypt a message with AES-GCM cipher
        /// </summary>
        /// <param name="payload">The message to decrypt</param>
        /// <param name="key">The key to decrypt it with it</param>
        /// <param name="nonce">The nonce (12 bytes)</param>
        /// <returns>Decrypted message</returns>
        public static byte[] AesGcmDecrypt(byte[] payload, byte[] key,byte[] nonce)
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(false,new AeadParameters(new KeyParameter(key), 128, nonce));
            // https://github.com/SidShetye/BouncyBench/blob/master/BouncyBench/Ciphers/AesGcm.cs#L209
            var clearBytes = new byte[cipher.GetOutputSize(payload.Length)];
            int len = cipher.ProcessBytes(payload, 0, payload.Length, clearBytes, 0);
            cipher.DoFinal(clearBytes, len);
            return clearBytes;
        }
    }
}
