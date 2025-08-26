using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Cross-platform data protection implementation
/// Uses Windows DPAPI on Windows and AES encryption on other platforms
/// </summary>
public class CrossPlatformDataProtection : ICrossPlatformDataProtection
{
    private readonly ILogger? _logger;
    private const int KeySize = 256; // AES-256
    private const int IvSize = 128; // 128-bit IV
    private const int SaltSize = 32; // 256-bit salt
    private const int Iterations = 100000; // PBKDF2 iterations
    
    public CrossPlatformDataProtection(ILogger? logger = null)
    {
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
        var salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }
        
        // Derive key from entropy and salt using PBKDF2
        using var pbkdf2 = new Rfc2898DeriveBytes(entropy, salt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize / 8);
        var iv = pbkdf2.GetBytes(IvSize / 8);
        
        // Encrypt data using AES
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = IvSize;
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
        
        if (combinedData.Length < SaltSize)
        {
            throw new CryptographicException("Invalid encrypted data format");
        }
        
        // Extract salt and encrypted data
        var salt = new byte[SaltSize];
        var encryptedBytes = new byte[combinedData.Length - SaltSize];
        Array.Copy(combinedData, 0, salt, 0, SaltSize);
        Array.Copy(combinedData, SaltSize, encryptedBytes, 0, encryptedBytes.Length);
        
        // Derive key from entropy and salt using PBKDF2
        using var pbkdf2 = new Rfc2898DeriveBytes(entropy, salt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize / 8);
        var iv = pbkdf2.GetBytes(IvSize / 8);
        
        // Decrypt data using AES
        using var aes = Aes.Create();
        aes.KeySize = KeySize;
        aes.BlockSize = IvSize;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = key;
        aes.IV = iv;
        
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
    }
}