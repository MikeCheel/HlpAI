namespace HlpAI.Services;

/// <summary>
/// Interface for cross-platform data protection services
/// </summary>
public interface ICrossPlatformDataProtection
{
    /// <summary>
    /// Encrypts data using platform-appropriate encryption
    /// </summary>
    /// <param name="data">Data to encrypt</param>
    /// <param name="entropy">Additional entropy for encryption</param>
    /// <returns>Encrypted data as base64 string</returns>
    string Protect(byte[] data, byte[] entropy);
    
    /// <summary>
    /// Decrypts data using platform-appropriate decryption
    /// </summary>
    /// <param name="encryptedData">Encrypted data as base64 string</param>
    /// <param name="entropy">Additional entropy used during encryption</param>
    /// <returns>Decrypted data</returns>
    byte[] Unprotect(string encryptedData, byte[] entropy);
    
    /// <summary>
    /// Gets whether the current platform supports data protection
    /// </summary>
    bool IsSupported { get; }
}