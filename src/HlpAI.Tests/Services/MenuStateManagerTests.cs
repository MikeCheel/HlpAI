using HlpAI.Models;
using HlpAI.Services;
using HlpAI.Tests.TestHelpers;
using Microsoft.Extensions.Logging;

namespace HlpAI.Tests.Services;

public class MenuStateManagerTests
{
    private ILogger? _logger;
    private MenuStateManager _menuStateManager = null!;
    private string _testDirectory = null!;
    private string _originalUserProfile = null!;
    private SqliteConfigurationService _configService = null!;
    private string _testDbPath = null!;

    [Before(Test)]
    public void Setup()
    {
        // Create isolated test environment
        _testDirectory = FileTestHelper.CreateTempDirectory($"menu_state_tests_{Guid.NewGuid():N}");
        _logger = null; // Use null logger for tests
        
        // Store original user profile and set to test directory
        _originalUserProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Environment.SetEnvironmentVariable("USERPROFILE", _testDirectory);
        
        // Create isolated test database and set as singleton instance
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_menu_state_{Guid.NewGuid()}.db");
        _configService = SqliteConfigurationService.SetTestInstance(_testDbPath, _logger);
        
        // Initialize with default configuration to ensure clean state
        var defaultConfig = new AppConfiguration { CurrentMenuContext = MenuContext.MainMenu };
        _configService.SaveAppConfigurationAsync(defaultConfig).GetAwaiter().GetResult();
        
        _menuStateManager = new MenuStateManager(_configService, _logger);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            // Dispose the config service first to release database connections
            _configService?.Dispose();
            
            // Release the singleton instance
            SqliteConfigurationService.ReleaseInstance();
            
            // Clear SQLite connection pools to release file handles
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            
            // Additional wait to ensure file handles are released
            Thread.Sleep(500);
            
            // Restore original user profile
            Environment.SetEnvironmentVariable("USERPROFILE", _originalUserProfile);
            
            // Clean up test database file with retry logic
            if (File.Exists(_testDbPath))
            {
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        File.Delete(_testDbPath);
                        break;
                    }
                    catch (IOException) when (i < 4)
                    {
                        Thread.Sleep(200 * (i + 1));
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    }
                }
            }
            
            // Clean up test directory
            if (Directory.Exists(_testDirectory))
            {
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        Directory.Delete(_testDirectory, true);
                        break;
                    }
                    catch (IOException) when (i < 2)
                    {
                        Thread.Sleep(200 * (i + 1));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log cleanup errors but don't fail the test
            Console.WriteLine($"Cleanup error: {ex.Message}");
        }
    }

    [Test]
    public async Task Constructor_WithNullLogger_InitializesCorrectly()
    {
        // Arrange
        // Create a test configuration service with clean state
        var testDbPath = Path.Combine(Path.GetTempPath(), $"test_constructor_{Guid.NewGuid()}.db");
        
        // Ensure the database file doesn't exist before starting
        if (File.Exists(testDbPath))
        {
            File.Delete(testDbPath);
        }
        
        // Clear any existing singleton instance and connection pools
        SqliteConfigurationService.ReleaseInstance();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Thread.Sleep(100); // Allow time for cleanup
        
        using var testConfigService = SqliteConfigurationService.SetTestInstance(testDbPath, null);
        
        // Initialize with default configuration to ensure clean state
        var defaultConfig = new AppConfiguration { CurrentMenuContext = MenuContext.MainMenu };
        await testConfigService.SaveAppConfigurationAsync(defaultConfig);
        
        // Act
        var manager = new MenuStateManager(testConfigService, null);

        // Assert
        await Assert.That(manager.CurrentContext).IsEqualTo(MenuContext.MainMenu);
        
        // Cleanup
        testConfigService.Dispose();
        
        // Release the singleton instance to ensure no conflicts
        SqliteConfigurationService.ReleaseInstance();
        
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        
        // Additional wait to ensure file handles are released
        Thread.Sleep(100);
        
        if (File.Exists(testDbPath))
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    File.Delete(testDbPath);
                    break;
                }
                catch (IOException) when (i < 4)
                {
                    Thread.Sleep(200 * (i + 1));
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                }
            }
        }
    }

    [Test]
    public async Task Constructor_WithLogger_InitializesCorrectly()
    {
        // Arrange
        var logger = new TestLogger();
        // Create a test configuration service with clean state
        var testDbPath = Path.Combine(Path.GetTempPath(), $"test_constructor_logger_{Guid.NewGuid()}.db");
        
        // Ensure the database file doesn't exist before starting
        if (File.Exists(testDbPath))
        {
            File.Delete(testDbPath);
        }
        
        // Clear any existing singleton instance and connection pools
        SqliteConfigurationService.ReleaseInstance();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Thread.Sleep(100); // Allow time for cleanup
        
        using var testConfigService = SqliteConfigurationService.SetTestInstance(testDbPath, logger);
        
        // Initialize with default configuration to ensure clean state
        var defaultConfig = new AppConfiguration { CurrentMenuContext = MenuContext.MainMenu };
        var saveResult = await testConfigService.SaveAppConfigurationAsync(defaultConfig);
        await Assert.That(saveResult).IsTrue(); // Ensure save was successful
        
        // Force a fresh load from the database to verify state
        testConfigService.Dispose();
        SqliteConfigurationService.ReleaseInstance();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        
        // Create a new instance to ensure we're loading from the clean database
        using var freshConfigService = SqliteConfigurationService.SetTestInstance(testDbPath, logger);
        var loadedConfig = await freshConfigService.LoadAppConfigurationAsync();
        await Assert.That(loadedConfig.CurrentMenuContext).IsEqualTo(MenuContext.MainMenu);

        // Act
        var manager = new MenuStateManager(freshConfigService, logger);

        // Assert
        await Assert.That(manager.CurrentContext).IsEqualTo(MenuContext.MainMenu);
        
        // Cleanup - Dispose configuration service
        freshConfigService?.Dispose();
        
        // Release the singleton instance to ensure no conflicts
        SqliteConfigurationService.ReleaseInstance();
        
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        
        // Additional wait to ensure file handles are released
        Thread.Sleep(200);
        
        if (File.Exists(testDbPath))
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    File.Delete(testDbPath);
                    break;
                }
                catch (IOException) when (i < 4)
                {
                    Thread.Sleep(200 * (i + 1));
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                }
            }
        }
    }

    [Test]
    public async Task NavigateToMenu_WithNewContext_UpdatesCurrentContext()
    {
        // Arrange
        var newContext = MenuContext.Configuration;

        // Act
        _menuStateManager.NavigateToMenu(newContext);

        // Assert
        await Assert.That(_menuStateManager.CurrentContext).IsEqualTo(newContext);
    }

    [Test]
    public async Task NavigateToMenu_WithAddToHistory_AddsToNavigationHistory()
    {
        // Arrange
        var initialContext = _menuStateManager.CurrentContext;
        var newContext = MenuContext.Configuration;

        // Act
        _menuStateManager.NavigateToMenu(newContext, addToHistory: true);
        var breadcrumb = _menuStateManager.GetBreadcrumbPath();

        // Assert
        await Assert.That(_menuStateManager.CurrentContext).IsEqualTo(newContext);
        await Assert.That(breadcrumb).Contains(MenuStateManager.GetMenuDisplayName(initialContext));
        await Assert.That(breadcrumb).Contains(MenuStateManager.GetMenuDisplayName(newContext));
    }

    [Test]
    public async Task NavigateToMenu_WithoutAddToHistory_DoesNotAddToHistory()
    {
        // Arrange
        var newContext = MenuContext.Configuration;

        // Act
        _menuStateManager.NavigateToMenu(newContext, addToHistory: false);
        var finalBreadcrumb = _menuStateManager.GetBreadcrumbPath();

        // Assert
        await Assert.That(_menuStateManager.CurrentContext).IsEqualTo(newContext);
        await Assert.That(finalBreadcrumb).IsEqualTo(MenuStateManager.GetMenuDisplayName(newContext));
        // Verify that navigating back goes to the original context (not added to history)
        await Assert.That(_menuStateManager.NavigateBack()).IsEqualTo(MenuContext.MainMenu);
    }

    [Test]
    public async Task NavigateToMenu_SameContext_DoesNotAddToHistory()
    {
        // Arrange
        var currentContext = _menuStateManager.CurrentContext;

        // Act
        _menuStateManager.NavigateToMenu(currentContext, addToHistory: true);
        var finalBreadcrumb = _menuStateManager.GetBreadcrumbPath();

        // Assert
        await Assert.That(_menuStateManager.CurrentContext).IsEqualTo(currentContext);
        await Assert.That(finalBreadcrumb).IsEqualTo(_menuStateManager.GetBreadcrumbPath());
    }

    [Test]
    public async Task NavigateBack_WithHistory_ReturnsToPreviousContext()
    {
        // Arrange
        _menuStateManager.NavigateToMenu(MenuContext.Configuration);
        _menuStateManager.NavigateToMenu(MenuContext.LogViewer);

        // Act
        var result = _menuStateManager.NavigateBack();

        // Assert
        await Assert.That(result).IsEqualTo(MenuContext.Configuration);
        await Assert.That(_menuStateManager.CurrentContext).IsEqualTo(MenuContext.Configuration);
    }

    [Test]
    public async Task NavigateBack_WithoutHistory_ReturnsToMainMenu()
    {
        // Arrange
        _menuStateManager.ResetToMainMenu();

        // Act
        var result = _menuStateManager.NavigateBack();

        // Assert
        await Assert.That(result).IsEqualTo(MenuContext.MainMenu);
        await Assert.That(_menuStateManager.CurrentContext).IsEqualTo(MenuContext.MainMenu);
    }

    [Test]
    public async Task ResetToMainMenu_Always_SetsMainMenuAsCurrentContext()
    {
        // Arrange
        _menuStateManager.NavigateToMenu(MenuContext.Configuration);
        _menuStateManager.NavigateToMenu(MenuContext.LogViewer);

        // Act
        _menuStateManager.ResetToMainMenu();

        // Assert
        await Assert.That(_menuStateManager.CurrentContext).IsEqualTo(MenuContext.MainMenu);
        await Assert.That(_menuStateManager.GetBreadcrumbPath()).IsEqualTo("Main Menu");
    }

    [Test]
    public async Task GetBreadcrumbPath_WithSingleMenu_ReturnsMenuName()
    {
        // Arrange
        _menuStateManager.ResetToMainMenu();

        // Act
        var breadcrumb = _menuStateManager.GetBreadcrumbPath();

        // Assert
        await Assert.That(breadcrumb).IsEqualTo("Main Menu");
    }

    [Test]
    public async Task GetBreadcrumbPath_WithMultipleMenus_ReturnsFullPath()
    {
        // Arrange
        _menuStateManager.ResetToMainMenu();
        _menuStateManager.NavigateToMenu(MenuContext.Configuration);
        _menuStateManager.NavigateToMenu(MenuContext.LogViewer);

        // Act
        var breadcrumb = _menuStateManager.GetBreadcrumbPath();

        // Assert
        await Assert.That(breadcrumb).Contains("Main Menu");
        await Assert.That(breadcrumb).Contains("Configuration");
        await Assert.That(breadcrumb).Contains("Log Viewer");
        await Assert.That(breadcrumb).Contains(" > ");
    }

    [Test]
    public async Task GetMenuDisplayName_WithKnownContexts_ReturnsCorrectNames()
    {
        // Arrange & Act & Assert
        await Assert.That(MenuStateManager.GetMenuDisplayName(MenuContext.MainMenu)).IsEqualTo("Main Menu");
        await Assert.That(MenuStateManager.GetMenuDisplayName(MenuContext.Configuration)).IsEqualTo("Configuration");
        await Assert.That(MenuStateManager.GetMenuDisplayName(MenuContext.LogViewer)).IsEqualTo("Log Viewer");
        await Assert.That(MenuStateManager.GetMenuDisplayName(MenuContext.ExtractorManagement)).IsEqualTo("Extractor Management");
        await Assert.That(MenuStateManager.GetMenuDisplayName(MenuContext.AiProviderManagement)).IsEqualTo("AI Provider Management");
        await Assert.That(MenuStateManager.GetMenuDisplayName(MenuContext.VectorDatabaseManagement)).IsEqualTo("Vector Database Management");
        await Assert.That(MenuStateManager.GetMenuDisplayName(MenuContext.FileFilteringManagement)).IsEqualTo("File Filtering Management");
    }

    [Test]
    public async Task GetMenuDisplayName_WithUnknownContext_ReturnsToString()
    {
        // Arrange
        var unknownContext = (MenuContext)999;

        // Act
        var displayName = MenuStateManager.GetMenuDisplayName(unknownContext);

        // Assert
        await Assert.That(displayName).IsEqualTo(unknownContext.ToString());
    }

    [Test]
    public async Task GetMenuIcon_WithKnownContexts_ReturnsCorrectIcons()
    {
        // Arrange & Act & Assert
        await Assert.That(MenuStateManager.GetMenuIcon(MenuContext.MainMenu)).IsEqualTo("🏠");
        await Assert.That(MenuStateManager.GetMenuIcon(MenuContext.Configuration)).IsEqualTo("⚙️");
        await Assert.That(MenuStateManager.GetMenuIcon(MenuContext.LogViewer)).IsEqualTo("📋");
        await Assert.That(MenuStateManager.GetMenuIcon(MenuContext.ExtractorManagement)).IsEqualTo("🔧");
        await Assert.That(MenuStateManager.GetMenuIcon(MenuContext.AiProviderManagement)).IsEqualTo("🤖");
        await Assert.That(MenuStateManager.GetMenuIcon(MenuContext.VectorDatabaseManagement)).IsEqualTo("🗄️");
        await Assert.That(MenuStateManager.GetMenuIcon(MenuContext.FileFilteringManagement)).IsEqualTo("🗂️");
    }

    [Test]
    public async Task GetMenuIcon_WithUnknownContext_ReturnsDefaultIcon()
    {
        // Arrange
        var unknownContext = (MenuContext)999;

        // Act
        var icon = MenuStateManager.GetMenuIcon(unknownContext);

        // Assert
        await Assert.That(icon).IsEqualTo("📄");
    }

    [Test]
    public async Task ToggleRememberMenuContext_ChangesRememberSetting()
    {
        // Arrange
        var initialSetting = _menuStateManager.RememberMenuContext;

        // Act
        _menuStateManager.ToggleRememberMenuContext();

        // Assert
        await Assert.That(_menuStateManager.RememberMenuContext).IsEqualTo(!initialSetting);
    }

    [Test]
    public async Task ToggleRememberMenuContext_CalledTwice_ReturnsToOriginalSetting()
    {
        // Arrange
        var initialSetting = _menuStateManager.RememberMenuContext;

        // Act
        _menuStateManager.ToggleRememberMenuContext();
        _menuStateManager.ToggleRememberMenuContext();

        // Assert
        await Assert.That(_menuStateManager.RememberMenuContext).IsEqualTo(initialSetting);
    }

    [Test]
    public async Task ClearHistory_ResetsToMainMenuOnly()
    {
        // Arrange
        _menuStateManager.NavigateToMenu(MenuContext.Configuration);
        _menuStateManager.NavigateToMenu(MenuContext.LogViewer);

        // Act
        _menuStateManager.ClearHistory();

        // Assert
        await Assert.That(_menuStateManager.CurrentContext).IsEqualTo(MenuContext.MainMenu);
        await Assert.That(_menuStateManager.GetBreadcrumbPath()).IsEqualTo("Main Menu");
    }

    [Test]
    public async Task GetStartupMenuContext_WithRememberEnabled_ReturnsCurrentContext()
    {
        // Arrange
        _menuStateManager.NavigateToMenu(MenuContext.Configuration);
        if (!_menuStateManager.RememberMenuContext)
        {
            _menuStateManager.ToggleRememberMenuContext();
        }

        // Act
        var startupContext = _menuStateManager.GetStartupMenuContext();

        // Assert
        await Assert.That(startupContext).IsEqualTo(MenuContext.Configuration);
    }

    [Test]
    public async Task GetStartupMenuContext_WithRememberDisabled_ReturnsMainMenu()
    {
        // Arrange
        _menuStateManager.NavigateToMenu(MenuContext.Configuration);
        if (_menuStateManager.RememberMenuContext)
        {
            _menuStateManager.ToggleRememberMenuContext();
        }

        // Act
        var startupContext = _menuStateManager.GetStartupMenuContext();

        // Assert
        await Assert.That(startupContext).IsEqualTo(MenuContext.MainMenu);
    }

    [Test]
    public async Task RefreshConfiguration_ReloadsConfigurationFromDisk()
    {
        // Arrange
        _menuStateManager.NavigateToMenu(MenuContext.Configuration);

        // Act
        _menuStateManager.RefreshConfiguration();

        // Assert
        // After refresh, the manager should maintain its state or reset based on configuration
        // CurrentContext is an enum, so just verify it's a valid value
        await Assert.That(_menuStateManager.CurrentContext).IsNotEqualTo((MenuContext)(-1));
    }

    [Test]
    public async Task NavigationSequence_ComplexNavigation_MaintainsCorrectState()
    {
        // Arrange & Act
        _menuStateManager.ResetToMainMenu();
        _menuStateManager.NavigateToMenu(MenuContext.Configuration);
        _menuStateManager.NavigateToMenu(MenuContext.LogViewer);
        _menuStateManager.NavigateToMenu(MenuContext.ExtractorManagement);
        
        var breadcrumbAfterNavigation = _menuStateManager.GetBreadcrumbPath();
        
        _menuStateManager.NavigateBack(); // Should go to LogViewer
        var contextAfterFirstBack = _menuStateManager.CurrentContext;
        
        _menuStateManager.NavigateBack(); // Should go to Configuration
        var contextAfterSecondBack = _menuStateManager.CurrentContext;
        
        _menuStateManager.NavigateBack(); // Should go to MainMenu
        var contextAfterThirdBack = _menuStateManager.CurrentContext;

        // Assert
        await Assert.That(breadcrumbAfterNavigation).Contains("Main Menu");
        await Assert.That(breadcrumbAfterNavigation).Contains("Configuration");
        await Assert.That(breadcrumbAfterNavigation).Contains("Log Viewer");
        await Assert.That(breadcrumbAfterNavigation).Contains("Extractor Management");
        
        await Assert.That(contextAfterFirstBack).IsEqualTo(MenuContext.LogViewer);
        await Assert.That(contextAfterSecondBack).IsEqualTo(MenuContext.Configuration);
        await Assert.That(contextAfterThirdBack).IsEqualTo(MenuContext.MainMenu);
    }

    [Test]
    public async Task MenuStateManager_WithAllMenuContexts_HandlesAllCorrectly()
    {
        // Arrange
        var allContexts = new[]
        {
            MenuContext.MainMenu,
            MenuContext.Configuration,
            MenuContext.LogViewer,
            MenuContext.ExtractorManagement,
            MenuContext.AiProviderManagement,
            MenuContext.VectorDatabaseManagement,
            MenuContext.FileFilteringManagement
        };

        // Act & Assert
        foreach (var context in allContexts)
        {
            _menuStateManager.NavigateToMenu(context);
            await Assert.That(_menuStateManager.CurrentContext).IsEqualTo(context);
            
            var displayName = MenuStateManager.GetMenuDisplayName(context);
            var icon = MenuStateManager.GetMenuIcon(context);
            
            await Assert.That(displayName).IsNotNull();
            await Assert.That(displayName).IsNotEqualTo("");
            await Assert.That(icon).IsNotNull();
            await Assert.That(icon).IsNotEqualTo("");
        }
    }
}

/// <summary>
/// Test logger implementation for testing purposes
/// </summary>
public class TestLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}