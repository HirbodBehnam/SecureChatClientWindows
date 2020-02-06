using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
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
        /// Generate a 96 bit nonce for Aes encryption
        /// </summary>
        /// <returns>nonce</returns>
        public static byte[] AesGenerateNonce()
        {
            byte[] nonce = new byte[12];
            SharedStuff.SecureRandom.GetBytes(nonce);
            return nonce;
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
            byte[] tag = new byte[16];
            Buffer.BlockCopy(payload,payload.Length - 16,tag,0,16);
            Array.Resize(ref payload,payload.Length - 16); // get the real payload without tag
            return AESGCM.GcmDecrypt(payload, key, nonce, tag);
        }
        /// <summary>
        /// Encrypt a string with AES-GCM; Nonce is created randomly
        /// </summary>
        /// <param name="payload">The string to encrypt</param>
        /// <param name="key">The key to encrypt it with</param>
        /// <returns>Encrypted bytes in base64 format</returns>
        public static string AesGcmEncrypt(string payload, string key)
        {
            return Convert.ToBase64String(AesGcmEncrypt(Encoding.UTF8.GetBytes(payload), 
                Convert.FromBase64String(key)));
        }
        /// <summary>
        /// Encrypt a byte array with AES-GCM; Nonce is created randomly
        /// </summary>
        /// <param name="payload">The array to encrypt</param>
        /// <param name="key">The key to encrypt it with</param>
        /// <returns>Encrypted bytes</returns>
        public static byte[] AesGcmEncrypt(byte[] payload, byte[] key)
        {
            return AesGcmEncrypt(payload, key, AesGenerateNonce());
        }
        /// <summary>
        /// Encrypt a byte array with AES-GCM; Nonce is created randomly
        /// </summary>
        /// <param name="payload">The array to encrypt</param>
        /// <param name="key">The key to encrypt it with</param>
        /// <param name="nonce">The nonce to encrypt it with (must be 12 bytes)</param>
        /// <returns>Encrypted bytes</returns>
        public static byte[] AesGcmEncrypt(byte[] payload, byte[] key,byte[] nonce)
        {
            byte[] tag = new byte[16];
            byte[] encrypted = AESGCM.GcmEncrypt(payload,key,nonce,tag);
            return encrypted.Concat(tag).ToArray();
        }
        /// <summary>
        /// Encrypts a file with AesGcm
        /// </summary>
        /// <param name="input">The input file to encrypt</param>
        /// <param name="output">Output file to write the encrypted file</param>
        /// <param name="key">The key to encrypt data with it</param>
        /// <remarks>
        /// This function breaks file into 1MB chunks, and encrypts each one separately
        /// After each full chunk the length of it becomes 1024 * 1024 + 28 (28 = 12 + 16) (nonce + hmac)
        /// This means that the file size increases about 0.002%
        /// Obviously the last block's size is not 1024 * 1024 + 28
        /// First 4 bytes of file is the buffer size
        /// </remarks>
        public static void AesGcmEncrypt(FileInfo input, FileInfo output, byte[] key)
        {
            AesGcmEncrypt(input, output, key, 1024 * 1024);
        }
        /// <summary>
        /// Encrypts a file with AesGcm
        /// </summary>
        /// <param name="input">The input file to encrypt</param>
        /// <param name="output">Output file to write the encrypted file</param>
        /// <param name="key">The key to encrypt data with it</param>
        /// <param name="bufferSize">The buffer size that the input is read and encrypted</param>
        public static void AesGcmEncrypt(FileInfo input, FileInfo output, byte[] key, int bufferSize)
        {
            using (Stream reader = input.OpenRead())
            {
                using (Stream writer = output.OpenWrite())
                {
                    // at first write the buffer size to first of file
                    writer.Write(SharedStuff.IntToBytes(bufferSize),0,4); // int is 4 bytes
                    // now read the input file; "bufferSize" bytes at a time
                    int readCount;
                    byte[] readBytes = new byte[bufferSize];
                    while ((readCount = reader.Read(readBytes,0,readBytes.Length)) > 0)
                    {
                        if(readBytes.Length > readCount)
                            Array.Resize(ref readBytes,readCount); // this is the last chunk of file; Do not encrypt all of the data in readBytes
                        byte[] crypted = AesGcmEncrypt(readBytes, key);
                        writer.Write(crypted,0,crypted.Length);
                    }
                }
            }
        }
        /// <summary>
        /// Reads and decrypts a file THAT IS ENCRYPTED WITH <see cref="AesGcmEncrypt(FileInfo,FileInfo,byte[],int)"/>
        /// </summary>
        /// <param name="input">The input file to decrypt it</param>
        /// <param name="output">The output file to write the decrypted data into it</param>
        /// <param name="key">Encryption key</param>
        public static void AesGcmDecrypt(FileInfo input, FileInfo output, byte[] key)
        {
            using (Stream reader = input.OpenRead())
            {
                using (Stream writer = output.OpenWrite())
                {
                    int bufferSize;
                    // at first read the crypt buffer
                    {
                        byte[] buffer = new byte[4];
                        reader.Read(buffer, 0, buffer.Length);
                        bufferSize = SharedStuff.BytesToInt(buffer);
                        bufferSize += 12 + 16; // hmac + nonce
                    }
                    // now read the input file; "bufferSize" bytes at a time
                    int readCount;
                    byte[] readBytes = new byte[bufferSize];
                    while ((readCount = reader.Read(readBytes,0,readBytes.Length)) > 0)
                    {
                        if(readBytes.Length > readCount)
                            Array.Resize(ref readBytes,readCount); // this is the last chunk of file; Do not encrypt all of the data in readBytes
                        byte[] decrypted = AesGcmDecrypt(readBytes, key);
                        writer.Write(decrypted,0,decrypted.Length);
                    }
                }
            }
        }
    }
}
