using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using HlpAI.Models;

namespace HlpAI.Services;

/// <summary>
/// Cross-platform data protection implementation
/// Uses Windows DPAPI on Windows and AES encryption on other platforms
/// </summary>
public class CrossPlatformDataProtection : ICrossPlatformDataProtection
{
    private readonly ILogger? _logger;
    private readonly AppConfiguration _config;
    
    public CrossPlatformDataProtection(AppConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }
    
    public bool IsSupported => true; // Always supported across platforms
    
    public string Protect(byte[] data, byte[] entropy)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (entropy == null) throw new ArgumentNullException(nameof(entropy));
        
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return ProtectWindows(data, entropy);
            }
            else
            {
                return ProtectCrossPlatform(data, entropy);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to protect data");
            throw new CryptographicException("Failed to protect data", ex);
        }
    }
    
    public byte[] Unprotect(string encryptedData, byte[] entropy)
    {
        if (string.IsNullOrEmpty(encryptedData)) throw new ArgumentNullException(nameof(encryptedData));
        if (entropy == null) throw new ArgumentNullException(nameof(entropy));
        
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return UnprotectWindows(encryptedData, entropy);
            }
            else
            {
                return UnprotectCrossPlatform(encryptedData, entropy);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unprotect data");
            throw new CryptographicException("Failed to unprotect data", ex);
        }
    }
    
    [SupportedOSPlatform("windows")]
    private string ProtectWindows(byte[] data, byte[] entropy)
    {
        var encryptedBytes = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }
    
    [SupportedOSPlatform("windows")]
    private byte[] UnprotectWindows(string encryptedData, byte[] entropy)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedData);
        return ProtectedData.Unprotect(encryptedBytes, entropy, DataProtectionScope.CurrentUser);
    }
    
    private string ProtectCrossPlatform(byte[] data, byte[] entropy)
    {
        // Generate a random salt
        var salt = new byte[_config.EncryptionSaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        
        // Derive key from entropy and salt using PBKDF2
        using var pbkdf2 = new Rfc2898DeriveBytes(entropy, salt, _config.EncryptionPbkdf2Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(_config.EncryptionKeySize / 8);
        var iv = pbkdf2.GetBytes(_config.EncryptionIvSize / 8);
        
        // Encrypt data using AES
        using var aes = Aes.Create();
        aes.KeySize = _config.EncryptionKeySize;
        aes.BlockSize = _config.EncryptionIvSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;
        
        using var encryptor = aes.CreateEncryptor();
        var encryptedData = encryptor.TransformFinalBlock(data, 0, data.Length);
        
        // Combine salt and encrypted data
        var result = new byte[salt.Length + encryptedData.Length];
        Array.Copy(salt, 0, result, 0, salt.Length);
        Array.Copy(encryptedData, 0, result, salt.Length, encryptedData.Length);
        
        return Convert.ToBase64String(result);
    }
    
    private byte[] UnprotectCrossPlatform(string encryptedData, byte[] entropy)
    {
        var combinedData = Convert.FromBase64String(encryptedData);
        
        if (combinedData.Length < _config.EncryptionSaltSize)
        {
            throw new CryptographicException("Invalid encrypted data format");
        }
        
        // Extract salt and encrypted data
        var salt = new byte[_config.EncryptionSaltSize];
        var encryptedBytes = new byte[combinedData.Length - _config.EncryptionSaltSize];
        Array.Copy(combinedData, 0, salt, 0, _config.EncryptionSaltSize);
        Array.Copy(combinedData, _config.EncryptionSaltSize, encryptedBytes, 0, encryptedBytes.Length);
        
        // Derive key from entropy and salt using PBKDF2
        using var pbkdf2 = new Rfc2898DeriveBytes(entropy, salt, _config.EncryptionPbkdf2Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(_config.EncryptionKeySize / 8);
        var iv = pbkdf2.GetBytes(_config.EncryptionIvSize / 8);
        
        // Decrypt data using AES
        using var aes = Aes.Create();
        aes.KeySize = _config.EncryptionKeySize;
        aes.BlockSize = _config.EncryptionIvSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;
        
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
    }
}