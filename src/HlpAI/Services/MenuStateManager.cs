using HlpAI.Models;
using Microsoft.Extensions.Logging;

namespace HlpAI.Services;

/// <summary>
/// Service for managing menu state and navigation in interactive mode
/// </summary>
public class MenuStateManager
{
    private readonly ILogger? _logger;
    private AppConfiguration _config;
    private Stack<MenuContext> _menuStack = null!;

    public MenuStateManager(ILogger? logger = null)
    {
        _logger = logger;
        _config = ConfigurationService.LoadConfiguration(_logger);
        InitializeMenuStack();
    }

    /// <summary>
    /// Constructor for testing that accepts a configuration service instance
    /// </summary>
    /// <param name="configService">The configuration service to use</param>
    /// <param name="logger">Optional logger</param>
    public MenuStateManager(SqliteConfigurationService configService, ILogger? logger = null)
    {
        _logger = logger;
        _config = configService.LoadAppConfigurationAsync().GetAwaiter().GetResult();
        InitializeMenuStack();
    }

    private void InitializeMenuStack()
    {
        _menuStack = new Stack<MenuContext>(_config.MenuHistory.AsEnumerable().Reverse());
        
        // Ensure main menu is always at the bottom of the stack
        if (_menuStack.Count == 0 || _menuStack.Last() != MenuContext.MainMenu)
        {
            _menuStack.Clear();
            _menuStack.Push(MenuContext.MainMenu);
            _config.CurrentMenuContext = MenuContext.MainMenu;
        }
    }

    /// <summary>
    /// Gets the current menu context
    /// </summary>
    public MenuContext CurrentContext => _config.CurrentMenuContext;

    /// <summary>
    /// Gets whether menu context should be remembered
    /// </summary>
    public bool RememberMenuContext => _config.RememberMenuContext;

    /// <summary>
    /// Navigates to a new menu context
    /// </summary>
    /// <param name="newContext">The menu context to navigate to</param>
    /// <param name="addToHistory">Whether to add the current context to navigation history</param>
    public void NavigateToMenu(MenuContext newContext, bool addToHistory = true)
    {
        if (addToHistory && _config.CurrentMenuContext != newContext)
        {
            _menuStack.Push(_config.CurrentMenuContext);
        }

        _config.CurrentMenuContext = newContext;
        UpdateMenuHistory();
        SaveConfiguration();
        
        _logger?.LogInformation("Navigated to menu: {MenuContext}", newContext);
    }

    /// <summary>
    /// Navigates back to the previous menu context
    /// </summary>
    /// <returns>The menu context that was navigated to, or MainMenu if no history</returns>
    public MenuContext NavigateBack()
    {
        if (_menuStack.Count > 0)
        {
            var previousContext = _menuStack.Pop();
            _config.CurrentMenuContext = previousContext;
        }
        else
        {
            _config.CurrentMenuContext = MenuContext.MainMenu;
        }

        UpdateMenuHistory();
        SaveConfiguration();
        
        _logger?.LogInformation("Navigated back to menu: {MenuContext}", _config.CurrentMenuContext);
        return _config.CurrentMenuContext;
    }

    /// <summary>
    /// Resets the menu state to the main menu
    /// </summary>
    public void ResetToMainMenu()
    {
        _menuStack.Clear();
        _config.CurrentMenuContext = MenuContext.MainMenu;
        UpdateMenuHistory();
        SaveConfiguration();
        
        _logger?.LogInformation("Reset menu state to MainMenu");
    }

    /// <summary>
    /// Gets the breadcrumb path for the current menu context
    /// </summary>
    /// <returns>A breadcrumb string showing the navigation path</returns>
    public string GetBreadcrumbPath()
    {
        var breadcrumbs = new List<string>();
        
        // If there's no navigation history or only MainMenu in stack, just show the current context
        if (_menuStack.Count == 0 || (_menuStack.Count == 1 && _menuStack.Peek() == MenuContext.MainMenu && _config.CurrentMenuContext != MenuContext.MainMenu))
        {
            return GetMenuDisplayName(_config.CurrentMenuContext);
        }
        
        // Build breadcrumb from the navigation stack
        foreach (var context in _menuStack.Reverse())
        {
            breadcrumbs.Add(GetMenuDisplayName(context));
        }
        
        // Add current context to show where we are now, but avoid duplicates
        var currentDisplayName = GetMenuDisplayName(_config.CurrentMenuContext);
        if (breadcrumbs.Count == 0 || breadcrumbs.Last() != currentDisplayName)
        {
            breadcrumbs.Add(currentDisplayName);
        }
        
        return string.Join(" > ", breadcrumbs);
    }

    /// <summary>
    /// Gets the display name for a menu context
    /// </summary>
    /// <param name="context">The menu context</param>
    /// <returns>The display name for the menu</returns>
    public static string GetMenuDisplayName(MenuContext context)
    {
        return context switch
        {
            MenuContext.MainMenu => "Main Menu",
            MenuContext.Configuration => "Configuration",
            MenuContext.LogViewer => "Log Viewer",
            MenuContext.ExtractorManagement => "Extractor Management",
            MenuContext.AiProviderManagement => "AI Provider Management",
            MenuContext.VectorDatabaseManagement => "Vector Database Management",
            MenuContext.FileFilteringManagement => "File Filtering Management",
            _ => context.ToString()
        };
    }

    /// <summary>
    /// Gets the header icon for a menu context
    /// </summary>
    /// <param name="context">The menu context</param>
    /// <returns>The icon for the menu header</returns>
    public static string GetMenuIcon(MenuContext context)
    {
        return context switch
        {
            MenuContext.MainMenu => "🏠",
            MenuContext.Configuration => "⚙️",
            MenuContext.LogViewer => "📋",
            MenuContext.ExtractorManagement => "🔧",
            MenuContext.AiProviderManagement => "🤖",
            MenuContext.VectorDatabaseManagement => "🗄️",
            MenuContext.FileFilteringManagement => "🗂️",
            _ => "📄"
        };
    }

    /// <summary>
    /// Toggles whether menu context should be remembered
    /// </summary>
    public void ToggleRememberMenuContext()
    {
        _config.RememberMenuContext = !_config.RememberMenuContext;
        SaveConfiguration();
        
        _logger?.LogInformation("Menu context remembering: {Enabled}", _config.RememberMenuContext ? "Enabled" : "Disabled");
    }

    /// <summary>
    /// Clears the menu navigation history
    /// </summary>
    public void ClearHistory()
    {
        _menuStack.Clear();
        _menuStack.Push(MenuContext.MainMenu);
        _config.CurrentMenuContext = MenuContext.MainMenu;
        UpdateMenuHistory();
        SaveConfiguration();
        
        _logger?.LogInformation("Cleared menu navigation history");
    }

    /// <summary>
    /// Gets the menu context that should be restored on startup
    /// </summary>
    /// <returns>The menu context to restore, or MainMenu if not remembering</returns>
    public MenuContext GetStartupMenuContext()
    {
        if (_config.RememberMenuContext)
        {
            return _config.CurrentMenuContext;
        }
        
        return MenuContext.MainMenu;
    }

    /// <summary>
    /// Updates the menu history in the configuration
    /// </summary>
    private void UpdateMenuHistory()
    {
        _config.MenuHistory = _menuStack.Reverse().ToList();
    }

    /// <summary>
    /// Saves the current configuration
    /// </summary>
    private void SaveConfiguration()
    {
        ConfigurationService.SaveConfiguration(_config, _logger);
    }

    /// <summary>
    /// Refreshes the configuration from disk
    /// </summary>
    public void RefreshConfiguration()
    {
        _config = ConfigurationService.LoadConfiguration(_logger);
        _menuStack.Clear();
        
        foreach (var context in _config.MenuHistory)
        {
            _menuStack.Push(context);
        }
        
        // Ensure main menu is always at the bottom
        if (_menuStack.Count == 0 || _menuStack.Last() != MenuContext.MainMenu)
        {
            _menuStack.Clear();
            _menuStack.Push(MenuContext.MainMenu);
        }
    }
}