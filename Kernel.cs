using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

using Sys = Cosmos.Kernel.System;

using Cosmos.Kernel.System.Graphics;
using Cosmos.Kernel.System.Graphics.Fonts;
using Cosmos.Kernel.System.Input;
using Cosmos.Kernel.System.Storage;
using Cosmos.Kernel.HAL.Devices.Storage;
using Cosmos.Kernel.System.Network;
using Cosmos.Kernel.System.FileSystem;
using Cosmos.Kernel.System.Diagnostics;

namespace customOS;

public class Kernel : Sys.Kernel
{
    private const string OS_NAME = "customOS";
    private const string OS_VERSION = "1.2.0";

    private const int TASKBAR_HEIGHT = 42;
    private const int TITLEBAR_HEIGHT = 30;

    private const int APP_NONE = 0;
    private const int APP_TERMINAL = 1;
    private const int APP_FILES = 2;
    private const int APP_SYSTEM = 3;
    private const int APP_SETTINGS = 4;
    private const int APP_NOTEPAD = 5;
    private const int APP_CALC = 6;
    private const int APP_CLOCK = 7;
    private const int APP_ABOUT = 8;

    private const int MENU_NONE = 0;
    private const int MENU_DESKTOP = 1;
    private const int MENU_ICON = 2;
    private const int MENU_TASKBAR = 3;
    private const int MENU_WINDOW = 4;
    private const int MENU_FILE = 5;

    private class Window
    {
        public int App;
        public string Title = "";
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public bool Minimized;
        public bool Maximized;
        public int RestoreX;
        public int RestoreY;
        public int RestoreWidth;
        public int RestoreHeight;
    }

    private class FileEntry
    {
        public string Path = "";
        public string Name = "";
        public bool IsDirectory;
        public long Size;
    }

    private DateTime bootTime;
    private int commandCount;

    private readonly List<string> commandHistory = new();

    private string terminalDirectory = "/mnt";
    private string filesDirectory = "/mnt";

    private Canvas? desktop;
    private PCScreenFont? uiFont;
    private int screenWidth;
    private int screenHeight;

    private bool guiRunning;
    private bool startMenuOpen;
    private int startMenuSelection;

    private readonly List<Window> windows = new();
    private int activeIndex = -1;

    private bool previousLeft;
    private bool previousRight;
    private bool previousMiddle;

    private Window? dragWindow;
    private int dragOffsetX;
    private int dragOffsetY;
    private Window? resizeWindow;

    private bool darkTheme = true;
    private int accentIndex = 0;
    private int wallpaperStyle = 0;
    private bool showSeconds = true;
    private float mouseSensitivity = 1.0f;

    private readonly Color[] accentPalette = new Color[]
    {
        Color.FromArgb(45, 125, 220),
        Color.FromArgb(0, 165, 155),
        Color.FromArgb(130, 95, 220),
        Color.FromArgb(230, 140, 45),
        Color.FromArgb(55, 175, 95),
        Color.FromArgb(220, 75, 90)
    };

    private static readonly int[] desktopApps = new int[]
    {
        APP_TERMINAL, APP_FILES, APP_SYSTEM, APP_SETTINGS,
        APP_NOTEPAD, APP_CALC, APP_CLOCK, APP_ABOUT
    };

    private readonly List<int> pinnedApps = new();
    private readonly List<int> taskbarApps = new();

    private bool contextMenuOpen;
    private int contextMenuX;
    private int contextMenuY;
    private int contextMenuKind;
    private int contextMenuTarget;
    private readonly List<string> contextLabels = new();
    private readonly List<int> contextActions = new();

    private string terminalInput = "";
    private readonly List<string> terminalOutput = new();
    private readonly List<string> terminalHistory = new();
    private int terminalHistoryIndex = 0;
    private int terminalScroll = 0;

    private readonly List<FileEntry> fileEntries = new();
    private int fileSelected = -1;
    private int fileScroll = 0;
    private string fileStatus = "";
    private long lastFileClickTicks = 0;
    private int lastFileClickIndex = -1;

    private int systemTab = 0;
    private long lastSystemRefresh = 0;
    private readonly List<string> systemLines = new();
    private readonly string[] systemTabNames = new string[]
    {
        "Overview", "Memory", "Storage", "Network", "Drivers"
    };

    private string settingsStatus = "";

    private string notepadText = "";
    private int notepadCaret = 0;
    private int notepadScrollLine = 0;
    private string notepadStatus = "";
    private string notepadPath = "/mnt/notes.txt";

    private string calcInput = "";
    private string calcResult = "0";

    private bool stopwatchRunning;
    private long stopwatchStartTicks;
    private long stopwatchAccumulatedTicks;

    private long lastTitleClickTicks = 0;
    private int lastTitleClickApp = APP_NONE;

    private Color Accent => accentPalette[accentIndex];

    private Color WindowColor => darkTheme
        ? Color.FromArgb(30, 38, 52)
        : Color.FromArgb(248, 250, 253);

    private Color PanelColor => darkTheme
        ? Color.FromArgb(22, 29, 41)
        : Color.FromArgb(232, 237, 245);

    private Color PanelAltColor => darkTheme
        ? Color.FromArgb(38, 48, 65)
        : Color.FromArgb(220, 227, 238);

    private Color TextColor => darkTheme
        ? Color.FromArgb(238, 242, 248)
        : Color.FromArgb(25, 32, 45);

    private Color MutedTextColor => darkTheme
        ? Color.FromArgb(155, 170, 190)
        : Color.FromArgb(95, 105, 120);

    private Color BorderColor => darkTheme
        ? Color.FromArgb(70, 85, 108)
        : Color.FromArgb(180, 190, 205);

    private Color TitleBarColor => darkTheme
        ? Color.FromArgb(36, 46, 62)
        : Color.FromArgb(225, 232, 242);

    private Color TaskbarColor => darkTheme
        ? Color.FromArgb(16, 22, 33)
        : Color.FromArgb(218, 225, 236);

    private Color TerminalBg => Color.FromArgb(10, 14, 20);

    // ==========================================
    // BOOT
    // ==========================================

    protected override void BeforeRun()
    {
        bootTime = DateTime.Now;

        pinnedApps.Clear();
        pinnedApps.Add(APP_TERMINAL);
        pinnedApps.Add(APP_FILES);
        pinnedApps.Add(APP_SYSTEM);
        pinnedApps.Add(APP_SETTINGS);
        pinnedApps.Add(APP_NOTEPAD);
        pinnedApps.Add(APP_CALC);

        LoadSettings();
        ApplySettings();

        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;

        Console.WriteLine(@"  ____           _             ___  ____");
        Console.WriteLine(@" / ___|   _ ___ | | ___  _ __ / _ \/ ___|");
        Console.WriteLine(@"| |  | | | / __|| __/ _ \| '_ \ | | \___ \");
        Console.WriteLine(@"| |__| |_| \__ \| || (_) | | | | |_| |___) |");
        Console.WriteLine(@" \_____\__,_|___/ \__\___/|_| |_|\___/|____/");

        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("             customOS v" + OS_VERSION);
        Console.WriteLine();
        Console.WriteLine("       System successfully initialized.");
        Console.WriteLine();

        Console.WriteLine("Type 'help' for commands.");
        Console.WriteLine("Type 'gui' to open the desktop.");
        Console.WriteLine();
    }

    // ==========================================
    // MAIN TERMINAL
    // ==========================================

    protected override void Run()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("customOS");

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write(" [" + terminalDirectory + "]");

        Console.ResetColor();
        Console.Write("> ");

        string input = Console.ReadLine() ?? "";

        if (string.IsNullOrWhiteSpace(input))
            return;

        commandHistory.Add(input);
        commandCount++;

        ExecuteCommand(input);

        Console.WriteLine();
    }

    private void ExecuteCommand(string input)
    {
        string[] parts = input.Split(' ', 2);

        string command = parts[0].ToLowerInvariant();
        string argument = parts.Length > 1 ? parts[1].Trim() : "";

        switch (command)
        {
            case "help":
                Help();
                break;

            case "about":
                About();
                break;

            case "gui":
                StartGUI();
                break;

            case "clear":
                Console.Clear();
                break;

            case "echo":
                Console.WriteLine(argument);
                break;

            case "time":
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss"));
                break;

            case "date":
                Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd"));
                break;

            case "uptime":
                Console.WriteLine(FormatDuration(DateTime.Now - bootTime));
                break;

            case "history":
                ShowHistory();
                break;

            case "version":
                Console.WriteLine("customOS version " + OS_VERSION);
                break;

            case "whoami":
                Console.WriteLine("root");
                break;

            case "commands":
                Console.WriteLine("Commands executed: " + commandCount);
                break;

            case "calc":
                Calculate(argument);
                break;

            case "sysinfo":
                ShowSystemInfo();
                break;

            case "memory":
            case "free":
            case "mem":
                ShowMemory();
                break;

            case "disks":
                ShowDisks();
                break;

            case "network":
            case "ipconfig":
                ShowNetwork();
                break;

            case "drivers":
                ShowDrivers();
                break;

            case "mounts":
                ShowMounts();
                break;

            case "df":
                ShowDiskUsage();
                break;

            case "neofetch":
                ShowNeofetch();
                break;

            case "ls":
            case "dir":
                ListDirectory(argument);
                break;

            case "cd":
                ChangeDirectory(argument);
                break;

            case "pwd":
                Console.WriteLine(terminalDirectory);
                break;

            case "cat":
                ReadFile(argument);
                break;

            case "write":
                WriteFile(argument);
                break;

            case "mkdir":
                CreateDirectory(argument);
                break;

            case "rm":
            case "del":
                DeleteFile(argument);
                break;

            case "rmdir":
                RemoveDirectory(argument);
                break;

            case "touch":
                TouchFile(argument);
                break;

            case "cp":
                CopyFile(argument);
                break;

            case "theme":
                darkTheme = !darkTheme;
                Console.WriteLine("Theme: " + (darkTheme ? "dark" : "light"));
                SaveSettings();
                break;

            case "wallpaper":
                wallpaperStyle = (wallpaperStyle + 1) % 5;
                Console.WriteLine("Wallpaper: " + GetWallpaperName());
                SaveSettings();
                break;

            case "settings":
                Console.WriteLine("Theme: " + (darkTheme ? "dark" : "light"));
                Console.WriteLine("Accent: " + accentIndex);
                Console.WriteLine("Wallpaper: " + GetWallpaperName());
                Console.WriteLine("Mouse sensitivity: " + mouseSensitivity.ToString("F1"));
                Console.WriteLine("Open 'gui' to edit these settings visually.");
                break;

            case "reboot":
                Reboot();
                break;

            case "shutdown":
            case "exit":
                Shutdown();
                break;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unknown command: " + command);
                Console.ResetColor();
                Console.WriteLine("Type 'help' for available commands.");
                break;
        }
    }

    // ==========================================
    // GUI STARTUP
    // ==========================================

    private void StartGUI()
    {
        try
        {
            desktop = Canvas.GetFullScreen();

            screenWidth = desktop.Width;
            screenHeight = desktop.Height;

            uiFont = LoadUiFont();

            MouseManager.SetScreenSize(screenWidth, screenHeight);
            ApplySettings();

            windows.Clear();
            activeIndex = -1;

            startMenuOpen = false;
            contextMenuOpen = false;
            startMenuSelection = 0;

            dragWindow = null;
            resizeWindow = null;

            previousLeft = MouseManager.LeftButton;
            previousRight = MouseManager.RightButton;
            previousMiddle = MouseManager.MiddleButton;

            terminalInput = "";
            terminalScroll = 0;
            terminalHistoryIndex = 0;
            terminalOutput.Clear();
            terminalHistory.Clear();

            AddTerminal("customOS Terminal v" + OS_VERSION);
            AddTerminal("Type 'help' for commands.");
            AddTerminal("Ctrl+Alt+T opens this window.");

            filesDirectory = "/mnt";
            RefreshFiles();

            LoadNotepad();

            lastSystemRefresh = 0;

            guiRunning = true;

            while (guiRunning)
            {
                HandleKeyboard();
                HandleMouse();

                DrawDesktop();

                Thread.Sleep(25);
            }
        }
        catch (Exception ex)
        {
            guiRunning = false;
            Console.WriteLine("GUI error: " + ex.Message);
        }

        try
        {
            if (desktop != null)
                Canvas.DisableFullScreen();
        }
        catch
        {
        }

        desktop = null;

        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("customOS Terminal");
        Console.ResetColor();

        Console.WriteLine("Returned to command line.");
        Console.WriteLine();
    }

    // ==========================================
    // DESKTOP
    // ==========================================

    private void DrawDesktop()
    {
        if (desktop == null)
            return;

        desktop.Clear(Color.FromArgb(10, 15, 24));

        DrawWallpaper();
        DrawDesktopIcons();

        for (int i = 0; i < windows.Count; i++)
            DrawWindow(windows[i]);

        DrawTaskbar();

        if (startMenuOpen)
            DrawStartMenu();

        if (contextMenuOpen)
            DrawContextMenu();

        DrawMouseCursor();

        desktop.Display();
    }

    private void DrawWallpaper()
    {
        if (desktop == null)
            return;

        switch (wallpaperStyle)
        {
            case 1:
                DrawGradient(
                    Color.FromArgb(7, 9, 18),
                    Color.FromArgb(30, 18, 50)
                );

                for (int i = 0; i < 70; i++)
                {
                    int sx = (i * 173 + 37) % screenWidth;
                    int sy = (i * 97 + 23) % (screenHeight - TASKBAR_HEIGHT - 30);
                    int size = (i % 3 == 0) ? 2 : 1;

                    desktop.DrawFilledRectangle(
                        Color.FromArgb(210, 220, 245),
                        sx, sy, size, size
                    );
                }
                break;

            case 2:
                DrawGradient(
                    Color.FromArgb(10, 34, 26),
                    Color.FromArgb(8, 20, 32)
                );

                desktop.DrawFilledCircle(
                    Color.FromArgb(16, 54, 40),
                    screenWidth / 5,
                    screenHeight - TASKBAR_HEIGHT - 20,
                    190
                );

                desktop.DrawFilledCircle(
                    Color.FromArgb(13, 46, 36),
                    screenWidth / 2,
                    screenHeight - TASKBAR_HEIGHT - 40,
                    230
                );

                desktop.DrawFilledCircle(
                    Color.FromArgb(10, 38, 30),
                    screenWidth - screenWidth / 5,
                    screenHeight - TASKBAR_HEIGHT,
                    200
                );
                break;

            case 3:
                DrawGradient(
                    Color.FromArgb(58, 20, 42),
                    Color.FromArgb(16, 12, 30)
                );

                desktop.DrawFilledCircle(
                    Color.FromArgb(235, 150, 70),
                    screenWidth - 150,
                    190,
                    70
                );

                desktop.DrawFilledCircle(
                    Color.FromArgb(245, 180, 95),
                    screenWidth - 150,
                    190,
                    48
                );
                break;

            case 4:
                desktop.DrawFilledRectangle(
                    Blend(Accent, Color.Black, 0.72f),
                    0, 0, screenWidth, screenHeight - TASKBAR_HEIGHT
                );
                break;

            default:
                DrawGradient(
                    Color.FromArgb(16, 34, 62),
                    Color.FromArgb(8, 20, 36)
                );

                desktop.DrawFilledCircle(
                    Color.FromArgb(24, 62, 108),
                    screenWidth - 130,
                    130,
                    170
                );

                desktop.DrawFilledCircle(
                    Color.FromArgb(20, 52, 92),
                    screenWidth - 40,
                    200,
                    110
                );

                desktop.DrawFilledCircle(
                    Color.FromArgb(20, 58, 92),
                    120,
                    screenHeight - TASKBAR_HEIGHT - 80,
                    140
                );
                break;
        }

        DrawText("customOS", Accent, 24, 16);
        DrawText("Desktop Environment v" + OS_VERSION, MutedTextColor, 24, 34);

        string stamp = DateTime.Now.ToString("yyyy-MM-dd  HH:mm");
        int stampWidth = MeasureString(stamp);

        DrawText(
            stamp,
            MutedTextColor,
            screenWidth - stampWidth - 20,
            screenHeight - TASKBAR_HEIGHT - 24
        );
    }

    private void DrawGradient(Color top, Color bottom)
    {
        if (desktop == null)
            return;

        int height = screenHeight - TASKBAR_HEIGHT;
        int bands = 40;

        for (int i = 0; i < bands; i++)
        {
            float t = (float)i / (bands - 1);

            Color color = Color.FromArgb(
                BlendChannel(top.R, bottom.R, t),
                BlendChannel(top.G, bottom.G, t),
                BlendChannel(top.B, bottom.B, t)
            );

            int y0 = (i * height) / bands;
            int y1 = ((i + 1) * height) / bands;

            desktop.DrawFilledRectangle(
                color, 0, y0, screenWidth, y1 - y0
            );
        }
    }

    private int BlendChannel(int first, int second, float amount)
    {
        int value = (int)(first + (second - first) * amount);

        if (value < 0)
            return 0;

        if (value > 255)
            return 255;

        return value;
    }

    private Color Blend(Color first, Color second, float amount)
    {
        return Color.FromArgb(
            BlendChannel(first.R, second.R, amount),
            BlendChannel(first.G, second.G, amount),
            BlendChannel(first.B, second.B, amount)
        );
    }

    // ==========================================
    // DESKTOP ICONS
    // ==========================================

    private void DrawDesktopIcons()
    {
        if (desktop == null)
            return;

        for (int i = 0; i < desktopApps.Length; i++)
        {
            int x;
            int y;
            GetDesktopIconRect(i, out x, out y);

            int app = desktopApps[i];
            bool hover = PointInRect(MouseManager.X, MouseManager.Y, x, y, 92, 88);

            DrawIconTile(
                x, y, 44,
                GetAppColor(app),
                GetAppGlyph(app),
                hover
            );

            string label = GetAppName(app);
            int labelWidth = MeasureString(label);

            DrawText(
                label,
                Color.White,
                x + (44 - labelWidth) / 2 + 1,
                y + 49
            );

            DrawText(
                label,
                Color.Black,
                x + (44 - labelWidth) / 2,
                y + 48
            );
        }
    }

    private void GetDesktopIconRect(int index, out int x, out int y)
    {
        int column = index % 2;
        int row = index / 2;

        x = 20 + column * 96;
        y = 74 + row * 92;
    }

    private void DrawIconTile(
        int x, int y, int size, Color color, string glyph, bool hover)
    {
        if (desktop == null)
            return;

        desktop.DrawFilledRectangle(
            Color.FromArgb(8, 12, 20),
            x + 3, y + 3, size, size
        );

        desktop.DrawFilledRectangle(color, x, y, size, size);

        desktop.DrawRectangle(
            hover ? Color.White : Blend(color, Color.White, 0.35f),
            x, y, size, size
        );

        int glyphWidth = MeasureString(glyph);

        DrawText(
            glyph,
            Color.White,
            x + (size - glyphWidth) / 2,
            y + (size - 16) / 2
        );
    }

    // ==========================================
    // TASKBAR
    // ==========================================

    private void DrawTaskbar()
    {
        if (desktop == null)
            return;

        BuildTaskbarApps();

        int y = screenHeight - TASKBAR_HEIGHT;

        desktop.DrawFilledRectangle(
            TaskbarColor, 0, y, screenWidth, TASKBAR_HEIGHT
        );

        desktop.DrawFilledRectangle(
            Blend(Accent, TaskbarColor, 0.55f),
            0, y, screenWidth, 1
        );

        bool startHover = PointInRect(
            MouseManager.X, MouseManager.Y, 4, y + 5, 78, 32
        );

        desktop.DrawFilledRectangle(
            startMenuOpen || startHover ? Accent : PanelAltColor,
            4, y + 5, 78, 32
        );

        desktop.DrawRectangle(
            Blend(Accent, Color.White, 0.2f),
            4, y + 5, 78, 32
        );

        desktop.DrawFilledRectangle(Accent, 10, y + 12, 16, 16);

        DrawText("C", Color.White, 14, y + 12);

        DrawText(
            "START",
            Color.White,
            31,
            y + 13
        );

        int buttonX = 88;

        for (int i = 0; i < taskbarApps.Count; i++)
        {
            int app = taskbarApps[i];
            bool pinned = IsPinned(app);
            int buttonWidth = pinned ? 40 : 118;

            if (buttonX + buttonWidth > screenWidth - 128)
                break;

            Window? window = FindWindow(app);
            bool active = window != null && window == ActiveWindow;
            bool hover = PointInRect(
                MouseManager.X, MouseManager.Y,
                buttonX, y + 4, buttonWidth, 34
            );

            Color buttonColor = active
                ? Blend(Accent, TaskbarColor, 0.25f)
                : (hover ? PanelAltColor : TaskbarColor);

            if (active || hover)
            {
                desktop.DrawFilledRectangle(
                    buttonColor, buttonX, y + 4, buttonWidth, 34
                );
            }

            DrawIconTile(
                buttonX + (pinned ? 8 : 7),
                y + 8,
                24,
                GetAppColor(app),
                GetAppGlyph(app),
                hover
            );

            if (!pinned)
            {
                DrawTextClipped(
                    GetAppName(app),
                    Color.White,
                    buttonX + 38,
                    y + 13,
                    buttonWidth - 44
                );
            }

            if (window != null)
            {
                desktop.DrawFilledRectangle(
                    active ? Color.White : Accent,
                    buttonX + 4,
                    y + TASKBAR_HEIGHT - 4,
                    buttonWidth - 8,
                    3
                );
            }

            buttonX += buttonWidth + 4;
        }

        string time = showSeconds
            ? DateTime.Now.ToString("HH:mm:ss")
            : DateTime.Now.ToString("HH:mm");

        string date = DateTime.Now.ToString("dd.MM.yyyy");

        int timeWidth = MeasureString(time);
        int dateWidth = MeasureString(date);

        DrawText(
            time,
            Color.White,
            screenWidth - timeWidth - 12,
            y + 6
        );

        DrawText(
            date,
            MutedTextColor,
            screenWidth - dateWidth - 12,
            y + 22
        );
    }

    private void BuildTaskbarApps()
    {
        taskbarApps.Clear();

        for (int i = 0; i < pinnedApps.Count; i++)
        {
            if (!taskbarApps.Contains(pinnedApps[i]))
                taskbarApps.Add(pinnedApps[i]);
        }

        for (int i = 0; i < windows.Count; i++)
        {
            if (!taskbarApps.Contains(windows[i].App))
                taskbarApps.Add(windows[i].App);
        }
    }

    private bool IsPinned(int app)
    {
        return pinnedApps.Contains(app);
    }

    private bool HitTestTaskbar(
        int mx, int my,
        out int app, out bool startButton, out bool clock)
    {
        app = APP_NONE;
        startButton = false;
        clock = false;

        int y = screenHeight - TASKBAR_HEIGHT;

        if (my < y || my > screenHeight)
            return false;

        if (PointInRect(mx, my, 4, y + 5, 78, 32))
        {
            startButton = true;
            return true;
        }

        if (mx >= screenWidth - 128)
        {
            clock = true;
            return true;
        }

        BuildTaskbarApps();

        int buttonX = 88;

        for (int i = 0; i < taskbarApps.Count; i++)
        {
            int currentApp = taskbarApps[i];
            int buttonWidth = IsPinned(currentApp) ? 40 : 118;

            if (buttonX + buttonWidth > screenWidth - 128)
                break;

            if (PointInRect(mx, my, buttonX, y + 4, buttonWidth, 34))
            {
                app = currentApp;
                return true;
            }

            buttonX += buttonWidth + 4;
        }

        return true;
    }

    // ==========================================
    // START MENU
    // ==========================================

    private int StartMenuItemCount => desktopApps.Length + 3;

    private void DrawStartMenu()
    {
        if (desktop == null)
            return;

        int width = 268;
        int headerHeight = 58;
        int itemHeight = 30;
        int footerHeight = 24;

        int height =
            headerHeight + StartMenuItemCount * itemHeight + footerHeight;

        int x = 4;
        int y = screenHeight - TASKBAR_HEIGHT - height - 4;

        desktop.DrawFilledRectangle(
            Color.FromArgb(8, 12, 20),
            x + 4, y + 4, width, height
        );

        desktop.DrawFilledRectangle(PanelColor, x, y, width, height);
        desktop.DrawRectangle(Accent, x, y, width, height);

        desktop.DrawFilledRectangle(
            Blend(Accent, PanelColor, 0.45f),
            x + 1, y + 1, width - 2, headerHeight - 2
        );

        desktop.DrawFilledCircle(Accent, x + 26, y + 25, 17);

        DrawText("C", Color.White, x + 22, y + 17);

        DrawText(OS_NAME, Color.White, x + 50, y + 12);
        DrawText(
            "Hobby OS v" + OS_VERSION,
            Color.FromArgb(220, 230, 245),
            x + 50, y + 32
        );

        desktop.DrawFilledRectangle(
            BorderColor, x + 8, y + headerHeight - 3, width - 16, 1
        );

        for (int i = 0; i < StartMenuItemCount; i++)
        {
            int itemX;
            int itemY;
            int itemW;
            int itemH;
            GetStartMenuItemRect(i, out itemX, out itemY, out itemW, out itemH);

            bool selected =
                i == startMenuSelection ||
                PointInRect(MouseManager.X, MouseManager.Y,
                    itemX, itemY, itemW, itemH);

            if (selected)
            {
                desktop.DrawFilledRectangle(
                    PanelAltColor, itemX, itemY, itemW, itemH
                );

                desktop.DrawFilledRectangle(
                    Accent, itemX, itemY, 3, itemH
                );
            }

            string label;
            string glyph;
            Color color;

            if (i < desktopApps.Length)
            {
                int app = desktopApps[i];

                label = GetAppName(app);
                glyph = GetAppGlyph(app);
                color = GetAppColor(app);
            }
            else
            {
                int action = i - desktopApps.Length;

                if (action == 0)
                {
                    label = "Exit Desktop";
                    glyph = "X";
                    color = Color.FromArgb(220, 75, 90);
                }
                else if (action == 1)
                {
                    label = "Restart";
                    glyph = "R";
                    color = Color.FromArgb(230, 140, 45);
                }
                else
                {
                    label = "Shut Down";
                    glyph = "P";
                    color = Color.FromArgb(120, 130, 150);
                }
            }

            DrawIconTile(
                itemX + 12, itemY + 5, 20, color, glyph, false
            );

            DrawText(label, TextColor, itemX + 40, itemY + 7);
        }

        int footerY = y + height - footerHeight;

        desktop.DrawFilledRectangle(
            Blend(PanelColor, Color.Black, 0.12f),
            x + 1, footerY, width - 2, footerHeight - 1
        );

        DrawText(
            "Arrow keys + Enter",
            MutedTextColor,
            x + 12, footerY + 4
        );
    }

    private void GetStartMenuItemRect(
        int index, out int x, out int y, out int width, out int height)
    {
        int menuWidth = 268;
        int headerHeight = 58;
        int itemHeight = 30;
        int footerHeight = 24;

        int menuHeight =
            headerHeight + StartMenuItemCount * itemHeight + footerHeight;

        int menuX = 4;
        int menuY = screenHeight - TASKBAR_HEIGHT - menuHeight - 4;

        x = menuX + 4;
        y = menuY + headerHeight + index * itemHeight;
        width = menuWidth - 8;
        height = itemHeight;
    }

    private void ActivateStartMenuItem(int index)
    {
        startMenuOpen = false;

        if (index < 0 || index >= StartMenuItemCount)
            return;

        if (index < desktopApps.Length)
        {
            OpenApplication(desktopApps[index]);
            return;
        }

        int action = index - desktopApps.Length;

        if (action == 0)
        {
            guiRunning = false;
        }
        else if (action == 1)
        {
            Reboot();
        }
        else
        {
            Shutdown();
        }
    }

    // ==========================================
    // CONTEXT MENUS
    // ==========================================

    private const int ACT_OPEN_APP = 1000;
    private const int ACT_TOGGLE_THEME = 2000;
    private const int ACT_NEXT_WALLPAPER = 2001;
    private const int ACT_SHOW_DESKTOP = 2002;
    private const int ACT_EXIT_DESKTOP = 2003;
    private const int ACT_MINIMIZE = 2004;
    private const int ACT_MAXIMIZE = 2005;
    private const int ACT_CLOSE = 2006;
    private const int ACT_FILE_OPEN = 2007;
    private const int ACT_FILE_DELETE = 2008;
    private const int ACT_FILE_NEW = 2009;
    private const int ACT_FILE_REFRESH = 2010;
    private const int ACT_PIN = 2011;
    private const int ACT_UNPIN = 2012;
    private const int ACT_ABOUT = 2013;
    private const int ACT_REBOOT = 2014;
    private const int ACT_SHUTDOWN = 2015;
    private const int ACT_SYSTEM_MONITOR = 2016;
    private const int ACT_SETTINGS = 2017;
    private const int ACT_SAVE_SETTINGS = 2018;

    private void OpenContextMenu(int kind, int target, int mx, int my)
    {
        contextLabels.Clear();
        contextActions.Clear();

        if (kind == MENU_DESKTOP)
        {
            AddContextItem("Open Terminal", ACT_OPEN_APP + APP_TERMINAL);
            AddContextItem("File Manager", ACT_OPEN_APP + APP_FILES);
            AddContextItem("System Monitor", ACT_OPEN_APP + APP_SYSTEM);
            AddContextItem("Settings", ACT_OPEN_APP + APP_SETTINGS);
            AddContextItem("", -1);
            AddContextItem("Change Wallpaper", ACT_NEXT_WALLPAPER);
            AddContextItem("Toggle Theme", ACT_TOGGLE_THEME);
            AddContextItem("", -1);
            AddContextItem("Show Desktop", ACT_SHOW_DESKTOP);
            AddContextItem("Exit Desktop", ACT_EXIT_DESKTOP);
        }
        else if (kind == MENU_ICON)
        {
            AddContextItem("Open", ACT_OPEN_APP + target);
            AddContextItem("", -1);
            AddContextItem("Pin to taskbar", ACT_PIN);
            AddContextItem("Unpin from taskbar", ACT_UNPIN);
            AddContextItem("Properties", ACT_ABOUT);
        }
        else if (kind == MENU_TASKBAR)
        {
            AddContextItem("Task Manager", ACT_SYSTEM_MONITOR);
            AddContextItem("Settings", ACT_SETTINGS);
            AddContextItem("", -1);
            AddContextItem("Show Desktop", ACT_SHOW_DESKTOP);
            AddContextItem("", -1);
            AddContextItem("Restart", ACT_REBOOT);
            AddContextItem("Shut Down", ACT_SHUTDOWN);
        }
        else if (kind == MENU_FILE)
        {
            AddContextItem("Open", ACT_FILE_OPEN);
            AddContextItem("Delete", ACT_FILE_DELETE);
            AddContextItem("", -1);
            AddContextItem("New Folder", ACT_FILE_NEW);
            AddContextItem("Refresh", ACT_FILE_REFRESH);
        }
        else
        {
            AddContextItem("Minimize", ACT_MINIMIZE);
            AddContextItem("Maximize / Restore", ACT_MAXIMIZE);
            AddContextItem("Close", ACT_CLOSE);
            AddContextItem("", -1);
            AddContextItem("Save settings", ACT_SAVE_SETTINGS);
        }

        int width = 204;
        int height = 12 + contextLabels.Count * 24;

        if (mx + width > screenWidth - 4)
            mx = screenWidth - width - 4;

        if (my + height > screenHeight - TASKBAR_HEIGHT - 4)
            my = screenHeight - TASKBAR_HEIGHT - height - 4;

        if (mx < 4)
            mx = 4;

        if (my < 4)
            my = 4;

        contextMenuX = mx;
        contextMenuY = my;
        contextMenuKind = kind;
        contextMenuTarget = target;
        contextMenuOpen = true;
        startMenuOpen = false;
    }

    private void AddContextItem(string label, int action)
    {
        contextLabels.Add(label);
        contextActions.Add(action);
    }

    private void DrawContextMenu()
    {
        if (desktop == null)
            return;

        int width = 204;
        int height = 12 + contextLabels.Count * 24;

        desktop.DrawFilledRectangle(
            Color.FromArgb(8, 12, 20),
            contextMenuX + 4, contextMenuY + 4, width, height
        );

        desktop.DrawFilledRectangle(
            PanelColor, contextMenuX, contextMenuY, width, height
        );

        desktop.DrawRectangle(
            Accent, contextMenuX, contextMenuY, width, height
        );

        int itemY = contextMenuY + 6;

        for (int i = 0; i < contextLabels.Count; i++)
        {
            string label = contextLabels[i];

            if (label.Length == 0)
            {
                desktop.DrawFilledRectangle(
                    BorderColor,
                    contextMenuX + 8,
                    itemY + 11,
                    width - 16,
                    1
                );

                itemY += 24;
                continue;
            }

            bool hover = PointInRect(
                MouseManager.X, MouseManager.Y,
                contextMenuX + 4, itemY, width - 8, 24
            );

            if (hover)
            {
                desktop.DrawFilledRectangle(
                    PanelAltColor,
                    contextMenuX + 4, itemY, width - 8, 24
                );

                desktop.DrawFilledRectangle(
                    Accent,
                    contextMenuX + 4, itemY, 3, 24
                );
            }

            DrawText(
                label,
                TextColor,
                contextMenuX + 16,
                itemY + 4
            );

            itemY += 24;
        }
    }

    private int GetContextMenuItemAt(int mx, int my)
    {
        int itemY = contextMenuY + 6;

        for (int i = 0; i < contextLabels.Count; i++)
        {
            if (PointInRect(mx, my, contextMenuX + 4, itemY, 196, 24))
                return i;

            itemY += 24;
        }

        return -1;
    }

    private void ExecuteContextAction(int action)
    {
        if (action >= ACT_OPEN_APP && action < ACT_OPEN_APP + 100)
        {
            OpenApplication(action - ACT_OPEN_APP);
            return;
        }

        switch (action)
        {
            case ACT_TOGGLE_THEME:
                darkTheme = !darkTheme;
                break;

            case ACT_NEXT_WALLPAPER:
                wallpaperStyle = (wallpaperStyle + 1) % 5;
                break;

            case ACT_SHOW_DESKTOP:
                ShowDesktop();
                break;

            case ACT_EXIT_DESKTOP:
                guiRunning = false;
                break;

            case ACT_MINIMIZE:
                if (ActiveWindow != null)
                    MinimizeWindow(ActiveWindow);

                break;

            case ACT_MAXIMIZE:
                if (ActiveWindow != null)
                    ToggleMaximize(ActiveWindow);

                break;

            case ACT_CLOSE:
                if (ActiveWindow != null)
                    CloseWindow(ActiveWindow);

                break;

            case ACT_FILE_OPEN:
                OpenFileEntry(contextMenuTarget);
                break;

            case ACT_FILE_DELETE:
                DeleteFileEntry(contextMenuTarget);
                break;

            case ACT_FILE_NEW:
                CreateNewFolder();
                break;

            case ACT_FILE_REFRESH:
                RefreshFiles();
                break;

            case ACT_PIN:
                if (!pinnedApps.Contains(contextMenuTarget))
                    pinnedApps.Add(contextMenuTarget);

                break;

            case ACT_UNPIN:
                pinnedApps.Remove(contextMenuTarget);
                break;

            case ACT_ABOUT:
                OpenApplication(APP_ABOUT);
                break;

            case ACT_REBOOT:
                Reboot();
                break;

            case ACT_SHUTDOWN:
                Shutdown();
                break;

            case ACT_SYSTEM_MONITOR:
                OpenApplication(APP_SYSTEM);
                break;

            case ACT_SETTINGS:
                OpenApplication(APP_SETTINGS);
                break;

            case ACT_SAVE_SETTINGS:
                SaveSettings();
                settingsStatus = "Settings saved.";
                break;
        }
    }

    // ==========================================
    // WINDOW MANAGEMENT
    // ==========================================

    private Window? FindWindow(int app)
    {
        for (int i = 0; i < windows.Count; i++)
        {
            if (windows[i].App == app)
                return windows[i];
        }

        return null;
    }

    private Window? ActiveWindow
    {
        get
        {
            if (activeIndex >= 0 && activeIndex < windows.Count)
                return windows[activeIndex];

            return null;
        }
    }

    private void OpenApplication(int app)
    {
        if (app == APP_NONE)
            return;

        Window? existing = FindWindow(app);

        if (existing != null)
        {
            existing.Minimized = false;
            FocusWindow(existing);

            startMenuOpen = false;
            contextMenuOpen = false;

            if (app == APP_FILES)
                RefreshFiles();

            if (app == APP_SYSTEM)
                lastSystemRefresh = 0;

            return;
        }

        Window window = new Window();

        window.App = app;
        window.Title = GetAppName(app);

        int width = GetDefaultWidth(app);
        int height = GetDefaultHeight(app);

        int maxWidth = screenWidth - 24;
        int maxHeight = screenHeight - TASKBAR_HEIGHT - 24;

        if (width > maxWidth)
            width = maxWidth;

        if (height > maxHeight)
            height = maxHeight;

        int offset = windows.Count * 22;

        window.X = (screenWidth - width) / 2 + offset - 30;
        window.Y = (screenHeight - TASKBAR_HEIGHT - height) / 2 + offset - 30;

        if (window.X < 8)
            window.X = 8;

        if (window.Y < 8)
            window.Y = 8;

        if (window.X + width > screenWidth - 8)
            window.X = screenWidth - width - 8;

        if (window.Y + height > screenHeight - TASKBAR_HEIGHT - 8)
            window.Y = screenHeight - TASKBAR_HEIGHT - height - 8;

        window.Width = width;
        window.Height = height;

        windows.Add(window);
        activeIndex = windows.Count - 1;

        startMenuOpen = false;
        contextMenuOpen = false;

        if (app == APP_TERMINAL)
            terminalScroll = 0;

        if (app == APP_FILES)
        {
            filesDirectory = "/mnt";
            RefreshFiles();
        }

        if (app == APP_SYSTEM)
            lastSystemRefresh = 0;

        if (app == APP_NOTEPAD)
            LoadNotepad();

        if (app == APP_SETTINGS)
            settingsStatus = "";
    }

    private void FocusWindow(Window window)
    {
        int index = windows.IndexOf(window);

        if (index < 0)
            return;

        if (index != windows.Count - 1)
        {
            windows.RemoveAt(index);
            windows.Add(window);
        }

        activeIndex = windows.Count - 1;
    }

    private void CloseWindow(Window window)
    {
        int index = windows.IndexOf(window);

        if (index < 0)
            return;

        windows.RemoveAt(index);

        if (dragWindow == window)
            dragWindow = null;

        if (resizeWindow == window)
            resizeWindow = null;

        activeIndex = -1;

        for (int i = windows.Count - 1; i >= 0; i--)
        {
            if (!windows[i].Minimized)
            {
                activeIndex = i;
                break;
            }
        }
    }

    private void MinimizeWindow(Window window)
    {
        window.Minimized = true;

        if (dragWindow == window)
            dragWindow = null;

        if (resizeWindow == window)
            resizeWindow = null;

        activeIndex = -1;

        for (int i = windows.Count - 1; i >= 0; i--)
        {
            if (!windows[i].Minimized)
            {
                activeIndex = i;
                break;
            }
        }
    }

    private void ToggleMaximize(Window window)
    {
        if (window.Maximized)
        {
            window.X = window.RestoreX;
            window.Y = window.RestoreY;
            window.Width = window.RestoreWidth;
            window.Height = window.RestoreHeight;
            window.Maximized = false;

            if (window.X < 0)
                window.X = 0;

            if (window.Y < 0)
                window.Y = 0;

            if (window.Width > screenWidth)
                window.Width = screenWidth;

            if (window.Height > screenHeight - TASKBAR_HEIGHT)
                window.Height = screenHeight - TASKBAR_HEIGHT;
        }
        else
        {
            window.RestoreX = window.X;
            window.RestoreY = window.Y;
            window.RestoreWidth = window.Width;
            window.RestoreHeight = window.Height;

            window.X = 0;
            window.Y = 0;
            window.Width = screenWidth;
            window.Height = screenHeight - TASKBAR_HEIGHT;
            window.Maximized = true;
        }

        FocusWindow(window);
    }

    private void ShowDesktop()
    {
        for (int i = 0; i < windows.Count; i++)
            windows[i].Minimized = true;

        activeIndex = -1;
        startMenuOpen = false;
        contextMenuOpen = false;
    }

    private void CycleWindows(int direction)
    {
        if (windows.Count == 0)
            return;

        int start = activeIndex;

        if (start < 0 || start >= windows.Count)
            start = windows.Count - 1;

        for (int step = 1; step <= windows.Count; step++)
        {
            int index = start + direction * step;

            while (index < 0)
                index += windows.Count;

            index = index % windows.Count;

            if (!windows[index].Minimized)
            {
                FocusWindow(windows[index]);
                return;
            }
        }
    }

    private void RefreshActiveApp()
    {
        Window? active = ActiveWindow;

        if (active == null)
            return;

        if (active.App == APP_FILES)
            RefreshFiles();

        if (active.App == APP_SYSTEM)
            lastSystemRefresh = 0;

        if (active.App == APP_NOTEPAD)
            LoadNotepad();
    }

    // ==========================================
    // WINDOW DRAWING
    // ==========================================

    private void DrawWindow(Window window)
    {
        if (desktop == null || window.Minimized)
            return;

        bool active = window == ActiveWindow;

        if (!window.Maximized)
        {
            desktop.DrawFilledRectangle(
                Color.FromArgb(6, 9, 15),
                window.X + 5,
                window.Y + 5,
                window.Width,
                window.Height
            );
        }

        desktop.DrawFilledRectangle(
            WindowColor,
            window.X, window.Y,
            window.Width, window.Height
        );

        desktop.DrawRectangle(
            active ? Accent : BorderColor,
            window.X, window.Y,
            window.Width, window.Height
        );

        desktop.DrawFilledRectangle(
            active ? Accent : TitleBarColor,
            window.X + 1,
            window.Y + 1,
            window.Width - 2,
            TITLEBAR_HEIGHT - 1
        );

        DrawIconTile(
            window.X + 7,
            window.Y + 7,
            16,
            GetAppColor(window.App),
            GetAppGlyph(window.App),
            false
        );

        DrawTextClipped(
            window.Title,
            active ? Color.White : TextColor,
            window.X + 29,
            window.Y + 7,
            window.Width - 125
        );

        int buttonY = window.Y + 4;
        int closeX = window.X + window.Width - 26;
        int maximizeX = closeX - 26;
        int minimizeX = maximizeX - 26;

        DrawWindowButton(
            closeX, buttonY, "X",
            Color.FromArgb(190, 55, 70),
            PointInRect(MouseManager.X, MouseManager.Y,
                closeX, buttonY, 22, 22)
        );

        DrawWindowButton(
            maximizeX, buttonY, "O",
            Blend(active ? Accent : TitleBarColor, Color.White, 0.18f),
            PointInRect(MouseManager.X, MouseManager.Y,
                maximizeX, buttonY, 22, 22)
        );

        DrawWindowButton(
            minimizeX, buttonY, "-",
            Blend(active ? Accent : TitleBarColor, Color.White, 0.12f),
            PointInRect(MouseManager.X, MouseManager.Y,
                minimizeX, buttonY, 22, 22)
        );

        DrawAppContent(window);

        if (!window.Maximized)
        {
            int gx = window.X + window.Width - 14;
            int gy = window.Y + window.Height - 14;

            desktop.DrawFilledRectangle(
                Blend(Accent, WindowColor, 0.45f),
                gx, gy, 12, 12
            );

            desktop.DrawLine(
                Color.White,
                gx + 4, gy + 11,
                gx + 11, gy + 4
            );

            desktop.DrawLine(
                Color.White,
                gx + 8, gy + 11,
                gx + 11, gy + 8
            );
        }
    }

    private void DrawWindowButton(
        int x, int y, string glyph, Color color, bool hover)
    {
        if (desktop == null)
            return;

        desktop.DrawFilledRectangle(
            hover ? Blend(color, Color.White, 0.22f) : color,
            x, y, 22, 22
        );

        desktop.DrawRectangle(
            Blend(color, Color.Black, 0.35f),
            x, y, 22, 22
        );

        int glyphWidth = MeasureString(glyph);

        DrawText(
            glyph,
            Color.White,
            x + (22 - glyphWidth) / 2,
            y + 3
        );
    }

    private int HitTestWindowButton(Window window, int mx, int my)
    {
        int buttonY = window.Y + 4;

        if (my < buttonY || my > buttonY + 22)
            return 0;

        int closeX = window.X + window.Width - 26;
        int maximizeX = closeX - 26;
        int minimizeX = maximizeX - 26;

        if (PointInRect(mx, my, closeX, buttonY, 22, 22))
            return 1;

        if (PointInRect(mx, my, maximizeX, buttonY, 22, 22))
            return 2;

        if (PointInRect(mx, my, minimizeX, buttonY, 22, 22))
            return 3;

        return 0;
    }

    private void DrawAppContent(Window window)
    {
        switch (window.App)
        {
            case APP_TERMINAL:
                DrawTerminal(window);
                break;

            case APP_FILES:
                DrawFileManager(window);
                break;

            case APP_SYSTEM:
                DrawSystemMonitor(window);
                break;

            case APP_SETTINGS:
                DrawSettings(window);
                break;

            case APP_NOTEPAD:
                DrawNotepad(window);
                break;

            case APP_CALC:
                DrawCalculator(window);
                break;

            case APP_CLOCK:
                DrawClock(window);
                break;

            case APP_ABOUT:
                DrawAbout(window);
                break;
        }
    }

    private int GetDefaultWidth(int app)
    {
        switch (app)
        {
            case APP_TERMINAL: return 700;
            case APP_FILES: return 760;
            case APP_SYSTEM: return 760;
            case APP_SETTINGS: return 700;
            case APP_NOTEPAD: return 720;
            case APP_CALC: return 380;
            case APP_CLOCK: return 440;
            case APP_ABOUT: return 640;
            default: return 600;
        }
    }

    private int GetDefaultHeight(int app)
    {
        switch (app)
        {
            case APP_TERMINAL: return 460;
            case APP_FILES: return 500;
            case APP_SYSTEM: return 520;
            case APP_SETTINGS: return 500;
            case APP_NOTEPAD: return 500;
            case APP_CALC: return 470;
            case APP_CLOCK: return 470;
            case APP_ABOUT: return 460;
            default: return 420;
        }
    }

    private string GetAppName(int app)
    {
        switch (app)
        {
            case APP_TERMINAL: return "Terminal";
            case APP_FILES: return "File Manager";
            case APP_SYSTEM: return "System Monitor";
            case APP_SETTINGS: return "Settings";
            case APP_NOTEPAD: return "Notepad";
            case APP_CALC: return "Calculator";
            case APP_CLOCK: return "Clock";
            case APP_ABOUT: return "About customOS";
            default: return "Application";
        }
    }

    private string GetAppGlyph(int app)
    {
        switch (app)
        {
            case APP_TERMINAL: return ">_";
            case APP_FILES: return "F";
            case APP_SYSTEM: return "S";
            case APP_SETTINGS: return "G";
            case APP_NOTEPAD: return "N";
            case APP_CALC: return "=";
            case APP_CLOCK: return "T";
            case APP_ABOUT: return "i";
            default: return "?";
        }
    }

    private Color GetAppColor(int app)
    {
        switch (app)
        {
            case APP_TERMINAL: return Color.FromArgb(45, 58, 78);
            case APP_FILES: return Color.FromArgb(200, 150, 40);
            case APP_SYSTEM: return Color.FromArgb(50, 150, 90);
            case APP_SETTINGS: return Color.FromArgb(110, 120, 140);
            case APP_NOTEPAD: return Color.FromArgb(60, 120, 200);
            case APP_CALC: return Color.FromArgb(120, 90, 200);
            case APP_CLOCK: return Color.FromArgb(0, 150, 145);
            case APP_ABOUT: return Color.FromArgb(200, 80, 90);
            default: return Color.FromArgb(100, 110, 125);
        }
    }

    // ==========================================
    // TERMINAL APP
    // ==========================================

    private void DrawTerminal(Window window)
    {
        if (desktop == null)
            return;

        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        desktop.DrawFilledRectangle(TerminalBg, x, y, width, height);

        int lineHeight = 16;
        int inputHeight = 30;
        int outputHeight = height - inputHeight - 8;
        int maxLines = outputHeight / lineHeight;

        if (maxLines < 1)
            maxLines = 1;

        int total = terminalOutput.Count;
        int maxScroll = total - maxLines;

        if (maxScroll < 0)
            maxScroll = 0;

        if (terminalScroll > maxScroll)
            terminalScroll = maxScroll;

        if (terminalScroll < 0)
            terminalScroll = 0;

        int start = total - maxLines - terminalScroll;

        if (start < 0)
            start = 0;

        int drawY = y + 6;

        for (int i = start; i < total && i < start + maxLines; i++)
        {
            DrawTextClipped(
                terminalOutput[i],
                Color.FromArgb(190, 225, 195),
                x + 8,
                drawY,
                width - 16
            );

            drawY += lineHeight;
        }

        int inputY = y + height - inputHeight;

        desktop.DrawFilledRectangle(
            Color.FromArgb(16, 22, 31),
            x, inputY, width, inputHeight
        );

        string prompt = terminalDirectory + "> " + terminalInput + "_";

        DrawTextClipped(
            prompt,
            TextColor,
            x + 8,
            inputY + 7,
            width - 16
        );

        if (terminalScroll > 0)
        {
            string indicator = "SCROLL " + terminalScroll;

            DrawText(
                indicator,
                Color.FromArgb(150, 170, 190),
                x + width - MeasureString(indicator) - 10,
                y + 4
            );
        }
    }

    private void AddTerminal(string text)
    {
        if (text == null)
            text = "";

        string[] lines = text.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Replace("\r", "");

            if (line.Length == 0)
            {
                terminalOutput.Add("");
                continue;
            }

            int position = 0;

            while (position < line.Length)
            {
                int length = Math.Min(84, line.Length - position);

                terminalOutput.Add(
                    line.Substring(position, length)
                );

                position += length;
            }
        }

        while (terminalOutput.Count > 400)
            terminalOutput.RemoveAt(0);

        terminalScroll = 0;
    }

    private void HandleTerminalKey(
        KeyEvent key, bool control, bool alt, bool shift)
    {
        if (control && key.Key == Key.L)
        {
            terminalOutput.Clear();
            terminalInput = "";
            return;
        }

        if (control && key.Key == Key.C)
        {
            terminalInput = "";
            return;
        }

        if (key.Key == Key.Enter)
        {
            string command = terminalInput.Trim();
            terminalInput = "";

            if (command.Length == 0)
                return;

            AddTerminal(terminalDirectory + "> " + command);

            terminalHistory.Add(command);
            terminalHistoryIndex = terminalHistory.Count;

            ProcessGuiCommand(command);
            return;
        }

        if (key.Key == Key.Backspace)
        {
            if (terminalInput.Length > 0)
            {
                terminalInput = terminalInput.Substring(
                    0, terminalInput.Length - 1
                );
            }

            return;
        }

        if (key.Key == Key.UpArrow)
        {
            if (terminalHistory.Count > 0)
            {
                if (terminalHistoryIndex > 0)
                    terminalHistoryIndex--;

                if (terminalHistoryIndex < terminalHistory.Count)
                    terminalInput = terminalHistory[terminalHistoryIndex];
            }

            return;
        }

        if (key.Key == Key.DownArrow)
        {
            if (terminalHistory.Count > 0)
            {
                if (terminalHistoryIndex < terminalHistory.Count - 1)
                {
                    terminalHistoryIndex++;
                    terminalInput = terminalHistory[terminalHistoryIndex];
                }
                else
                {
                    terminalHistoryIndex = terminalHistory.Count;
                    terminalInput = "";
                }
            }

            return;
        }

        if (key.Key == Key.PageUp)
        {
            terminalScroll += 5;
            return;
        }

        if (key.Key == Key.PageDown)
        {
            terminalScroll -= 5;

            if (terminalScroll < 0)
                terminalScroll = 0;

            return;
        }

        if (key.Key == Key.Tab)
        {
            terminalInput += "    ";
            return;
        }

        char character = key.KeyChar;

        if (character >= 32 && character <= 126 &&
            terminalInput.Length < 120)
        {
            terminalInput += character;
        }
    }

    private void ProcessGuiCommand(string input)
    {
        string[] parts = input.Split(' ', 2);

        string command = parts[0].ToLowerInvariant();
        string argument = parts.Length > 1 ? parts[1].Trim() : "";

        switch (command)
        {
            case "help":
                AddTerminal("customOS terminal commands:");
                AddTerminal("help clear echo time date uptime version whoami");
                AddTerminal("pwd ls cd cat write mkdir touch rm rmdir cp");
                AddTerminal("free mem df mounts disks drivers ipconfig net");
                AddTerminal("sysinfo neofetch ps theme wallpaper accent open");
                AddTerminal("settings exit reboot shutdown calc history");
                break;

            case "clear":
                terminalOutput.Clear();
                break;

            case "echo":
                AddTerminal(argument);
                break;

            case "time":
                AddTerminal(DateTime.Now.ToString("HH:mm:ss"));
                break;

            case "date":
                AddTerminal(DateTime.Now.ToString("yyyy-MM-dd"));
                break;

            case "uptime":
                AddTerminal("Uptime: " + FormatDuration(DateTime.Now - bootTime));
                break;

            case "version":
                AddTerminal("customOS " + OS_VERSION);
                break;

            case "whoami":
                AddTerminal("root");
                break;

            case "pwd":
                AddTerminal(terminalDirectory);
                break;

            case "ls":
            case "dir":
                ListTerminalDirectory(argument);
                break;

            case "cd":
                TerminalChangeDirectory(argument);
                break;

            case "cat":
                TerminalReadFile(argument);
                break;

            case "write":
                TerminalWriteFile(argument);
                break;

            case "mkdir":
                TerminalCreateDirectory(argument);
                break;

            case "touch":
                TerminalTouchFile(argument);
                break;

            case "rm":
            case "del":
                TerminalDeleteFile(argument);
                break;

            case "rmdir":
                TerminalRemoveDirectory(argument);
                break;

            case "cp":
                TerminalCopyFile(argument);
                break;

            case "free":
            case "mem":
            case "memory":
                TerminalMemory();
                break;

            case "df":
                TerminalDiskUsage();
                break;

            case "mounts":
                TerminalMounts();
                break;

            case "disks":
                TerminalDisks();
                break;

            case "drivers":
                TerminalDrivers();
                break;

            case "ipconfig":
            case "network":
            case "net":
                TerminalNetwork();
                break;

            case "sysinfo":
                AddTerminal("customOS " + OS_VERSION);
                AddTerminal("Kernel: Cosmos Gen3");
                AddTerminal("Architecture: " + RuntimeInformation.ProcessArchitecture);
                AddTerminal("CPU: " + GetCpuName());
                AddTerminal("Logical processors: " + Environment.ProcessorCount);
                AddTerminal("Display: " + screenWidth + "x" + screenHeight);
                AddTerminal("Uptime: " + FormatDuration(DateTime.Now - bootTime));
                break;

            case "neofetch":
                TerminalNeofetch();
                break;

            case "ps":
                TerminalProcesses();
                break;

            case "theme":
                darkTheme = !darkTheme;
                AddTerminal("Theme: " + (darkTheme ? "dark" : "light"));
                break;

            case "wallpaper":
                wallpaperStyle = (wallpaperStyle + 1) % 5;
                AddTerminal("Wallpaper: " + GetWallpaperName());
                break;

            case "accent":
                int requestedAccent;

                if (int.TryParse(argument, out requestedAccent))
                {
                    if (requestedAccent >= 0 &&
                        requestedAccent < accentPalette.Length)
                    {
                        accentIndex = requestedAccent;
                        AddTerminal("Accent set to " + accentIndex);
                    }
                    else
                    {
                        AddTerminal("Accent must be 0 to " +
                            (accentPalette.Length - 1));
                    }
                }
                else
                {
                    AddTerminal("Usage: accent 0-" +
                        (accentPalette.Length - 1));
                }
                break;

            case "open":
                OpenNamedApp(argument);
                break;

            case "settings":
                OpenApplication(APP_SETTINGS);
                AddTerminal("Opening Settings.");
                break;

            case "exit":
                Window? terminalWindow = FindWindow(APP_TERMINAL);

                if (terminalWindow != null)
                    CloseWindow(terminalWindow);

                break;

            case "reboot":
                Reboot();
                break;

            case "shutdown":
                Shutdown();
                break;

            case "calc":
                try
                {
                    double value = EvaluateExpression(argument);
                    AddTerminal("= " + value.ToString());
                }
                catch
                {
                    AddTerminal("Invalid expression.");
                }
                break;

            case "history":
                for (int i = 0; i < terminalHistory.Count; i++)
                    AddTerminal((i + 1) + ". " + terminalHistory[i]);
                break;

            case "commands":
                AddTerminal("Commands executed: " + commandCount);
                break;

            default:
                AddTerminal("Unknown command: " + command);
                AddTerminal("Type 'help' for available commands.");
                break;
        }
    }

    private void OpenNamedApp(string name)
    {
        string app = name.Trim().ToLowerInvariant();

        if (app == "terminal" || app == "term")
            OpenApplication(APP_TERMINAL);
        else if (app == "files" || app == "file" || app == "fm")
            OpenApplication(APP_FILES);
        else if (app == "system" || app == "monitor" || app == "sys")
            OpenApplication(APP_SYSTEM);
        else if (app == "settings" || app == "config")
            OpenApplication(APP_SETTINGS);
        else if (app == "notepad" || app == "notes" || app == "edit")
            OpenApplication(APP_NOTEPAD);
        else if (app == "calc" || app == "calculator")
            OpenApplication(APP_CALC);
        else if (app == "clock" || app == "time")
            OpenApplication(APP_CLOCK);
        else if (app == "about" || app == "info")
            OpenApplication(APP_ABOUT);
        else
            AddTerminal("Unknown app. Try: terminal, files, system, settings, notepad, calc, clock, about");
    }

    private void ListTerminalDirectory(string path)
    {
        string target = ResolvePath(terminalDirectory, path);

        try
        {
            if (!Directory.Exists(target))
            {
                AddTerminal("Directory not found: " + target);
                return;
            }

            AddTerminal("Directory: " + target);

            string[] directories = Directory.GetDirectories(target);
            string[] files = Directory.GetFiles(target);

            for (int i = 0; i < directories.Length; i++)
            {
                AddTerminal("[DIR]  " + GetNameFromPath(directories[i]));
            }

            for (int i = 0; i < files.Length; i++)
            {
                AddTerminal(
                    "[FILE] " + GetNameFromPath(files[i]) +
                    "  " + FormatBytes((ulong)GetFileSize(files[i]))
                );
            }

            if (directories.Length == 0 && files.Length == 0)
                AddTerminal("(empty)");
        }
        catch
        {
            AddTerminal("Cannot read directory.");
        }
    }

    private void TerminalChangeDirectory(string path)
    {
        string target;

        if (string.IsNullOrWhiteSpace(path) || path == "~")
            target = "/mnt";
        else
            target = ResolvePath(terminalDirectory, path);

        try
        {
            if (Directory.Exists(target))
            {
                terminalDirectory = NormalizePath(target);
                AddTerminal(terminalDirectory);
            }
            else
            {
                AddTerminal("Directory not found: " + target);
            }
        }
        catch
        {
            AddTerminal("Filesystem unavailable.");
        }
    }

    private void TerminalReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AddTerminal("Usage: cat filename");
            return;
        }

        try
        {
            string target = ResolvePath(terminalDirectory, path);

            if (!File.Exists(target))
            {
                AddTerminal("File not found: " + target);
                return;
            }

            string text = File.ReadAllText(target);
            string[] lines = text.Split('\n');

            for (int i = 0; i < lines.Length && i < 200; i++)
                AddTerminal(lines[i].Replace("\r", ""));

            if (lines.Length > 200)
                AddTerminal("... output truncated ...");
        }
        catch
        {
            AddTerminal("Cannot read file.");
        }
    }

    private void TerminalWriteFile(string argument)
    {
        int separator = argument.IndexOf(' ');

        if (separator <= 0)
        {
            AddTerminal("Usage: write filename text");
            return;
        }

        string path = ResolvePath(
            terminalDirectory,
            argument.Substring(0, separator)
        );

        string content = argument.Substring(separator + 1);

        try
        {
            File.WriteAllText(path, content);
            AddTerminal("Wrote " + content.Length + " chars to " + path);
        }
        catch
        {
            AddTerminal("Write failed.");
        }
    }

    private void TerminalCreateDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AddTerminal("Usage: mkdir name");
            return;
        }

        try
        {
            Directory.CreateDirectory(
                ResolvePath(terminalDirectory, path)
            );

            AddTerminal("Directory created.");
        }
        catch
        {
            AddTerminal("Could not create directory.");
        }
    }

    private void TerminalTouchFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AddTerminal("Usage: touch filename");
            return;
        }

        try
        {
            string target = ResolvePath(terminalDirectory, path);

            if (!File.Exists(target))
                File.WriteAllText(target, "");

            AddTerminal("Touched " + target);
        }
        catch
        {
            AddTerminal("Could not create file.");
        }
    }

    private void TerminalDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AddTerminal("Usage: rm filename");
            return;
        }

        try
        {
            string target = ResolvePath(terminalDirectory, path);

            if (File.Exists(target))
            {
                File.Delete(target);
                AddTerminal("Deleted " + target);
            }
            else
            {
                AddTerminal("File not found.");
            }
        }
        catch
        {
            AddTerminal("Delete failed.");
        }
    }

    private void TerminalRemoveDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            AddTerminal("Usage: rmdir name");
            return;
        }

        try
        {
            string target = ResolvePath(terminalDirectory, path);

            if (Directory.Exists(target))
            {
                Directory.Delete(target);
                AddTerminal("Removed " + target);
            }
            else
            {
                AddTerminal("Directory not found.");
            }
        }
        catch
        {
            AddTerminal("Directory must be empty or cannot be removed.");
        }
    }

    private void TerminalCopyFile(string argument)
    {
        string[] parts = argument.Split(
            ' ', StringSplitOptions.RemoveEmptyEntries
        );

        if (parts.Length < 2)
        {
            AddTerminal("Usage: cp source destination");
            return;
        }

        try
        {
            string source = ResolvePath(terminalDirectory, parts[0]);
            string destination = ResolvePath(terminalDirectory, parts[1]);

            if (!File.Exists(source))
            {
                AddTerminal("Source file not found.");
                return;
            }

            File.WriteAllText(destination, File.ReadAllText(source));
            AddTerminal("Copied to " + destination);
        }
        catch
        {
            AddTerminal("Copy failed.");
        }
    }

    private void TerminalMemory()
    {
        try
        {
            ulong ram = MemoryDiagnostics.RamSizeBytes;
            ulong pageSize = MemoryDiagnostics.PageSizeBytes;
            ulong totalPages = MemoryDiagnostics.TotalPages;
            ulong freePages = MemoryDiagnostics.FreePages;
            ulong usedPages = totalPages - freePages;

            AddTerminal("Physical RAM: " + FormatBytes(ram));
            AddTerminal("Page size: " + pageSize + " bytes");
            AddTerminal("Pages: " + usedPages + " used / " +
                totalPages + " total");
            AddTerminal("Free pages: " + freePages);

            if (totalPages > 0)
            {
                int percent = (int)(usedPages * 100 / totalPages);
                AddTerminal("Usage: " + MakeBar(percent, 24) + " " +
                    percent + "%");
            }

            AddTerminal("Managed heap: " +
                FormatBytes((ulong)GC.GetTotalMemory(false)));
            AddTerminal("GC time: " + MemoryDiagnostics.GcTimePercent + "%");
            AddTerminal("Collections: " +
                MemoryDiagnostics.TotalCollections);
        }
        catch
        {
            AddTerminal("Memory diagnostics unavailable.");
        }
    }

    private void TerminalMounts()
    {
        try
        {
            IReadOnlyList<VfsMount> mounts = VfsManager.Mounts;

            AddTerminal("Mounts: " + mounts.Count);

            for (int i = 0; i < mounts.Count; i++)
            {
                VfsMount mount = mounts[i];

                AddTerminal(
                    mount.MountPoint + " <- " + mount.Source +
                    (mount.IsReadOnly ? " (ro)" : " (rw)")
                );
            }
        }
        catch
        {
            AddTerminal("Mount information unavailable.");
        }
    }

    private void TerminalDiskUsage()
    {
        try
        {
            IReadOnlyList<VfsMount> mounts = VfsManager.Mounts;

            AddTerminal("Filesystem usage:");

            for (int i = 0; i < mounts.Count; i++)
            {
                string point = mounts[i].MountPoint;
                VfsStatFs stat = default(VfsStatFs);

                if (VfsManager.TryStatFs(point, out stat) &&
                    stat.BlockSize > 0)
                {
                    ulong total = stat.BlockSize * stat.Blocks;
                    ulong free = stat.BlockSize * stat.FreeBlocks;
                    ulong used = total - free;
                    int percent = total > 0
                        ? (int)(used * 100 / total)
                        : 0;

                    AddTerminal(
                        point + "  " + FormatBytes(used) + " / " +
                        FormatBytes(total) + "  " + percent + "%"
                    );
                }
                else
                {
                    AddTerminal(point + "  (usage unavailable)");
                }
            }
        }
        catch
        {
            AddTerminal("Disk usage unavailable.");
        }
    }

    private void TerminalDisks()
    {
        try
        {
            if (!StorageManager.IsEnabled)
            {
                AddTerminal("Storage subsystem is disabled.");
                return;
            }

            AddTerminal("Storage devices: " + StorageManager.DeviceCount);

            for (int i = 0; i < StorageManager.DeviceCount; i++)
            {
                IBlockDevice device = StorageManager.GetDevice(i);

                AddTerminal(
                    "[" + i + "] " + device.Name + "  " +
                    FormatBytes(device.BlockCount * device.BlockSize) +
                    "  (" + device.BlockCount + " x " +
                    device.BlockSize + ")"
                );
            }

            IReadOnlyList<Partition> partitions = StorageManager.Partitions;

            AddTerminal("Partitions: " + partitions.Count);

            for (int i = 0; i < partitions.Count; i++)
            {
                Partition partition = partitions[i];

                AddTerminal(
                    "  " + partition.Name + "  " +
                    FormatBytes(partition.BlockCount * partition.BlockSize)
                );
            }
        }
        catch
        {
            AddTerminal("Storage information unavailable.");
        }
    }

    private void TerminalNetwork()
    {
        try
        {
            if (!NetworkManager.IsEnabled)
            {
                AddTerminal("Network subsystem is disabled.");
                return;
            }

            AddTerminal("Network adapters: " + NetworkManager.DeviceCount);

            for (int i = 0; i < NetworkManager.DeviceCount; i++)
            {
                NetworkAdapter adapter = NetworkManager.GetAdapter(i);

                AddTerminal("[" + i + "] " + adapter.Name);
                AddTerminal("    MAC: " + adapter.MacAddress.ToString());
                AddTerminal("    Link: " + (adapter.LinkUp ? "up" : "down") +
                    "  Ready: " + adapter.Ready);

                IPConfig? config = adapter.IPConfig;

                if (config != null)
                {
                    AddTerminal("    IP: " + config.Address.ToString());
                    AddTerminal("    Mask: " +
                        config.SubnetMask.ToString());
                    AddTerminal("    Gateway: " +
                        config.DefaultGateway.ToString());
                }
            }

            AddTerminal("Hostname: " + DnsConfig.HostName);

            IReadOnlyList<Address> dns = DnsConfig.Nameservers;

            if (dns.Count > 0)
            {
                string servers = "";

                for (int i = 0; i < dns.Count; i++)
                {
                    if (i > 0)
                        servers += ", ";

                    servers += dns[i].ToString();
                }

                AddTerminal("DNS: " + servers);
            }
        }
        catch
        {
            AddTerminal("Network information unavailable.");
        }
    }

    private void TerminalDrivers()
    {
        try
        {
            if (!DriverDiagnostics.IsStarted)
            {
                AddTerminal("Driver diagnostics are not started.");
                return;
            }

            AddTerminal("Drivers: " + DriverDiagnostics.DriverCount);
            AddTerminal("Device nodes: " + DriverDiagnostics.NodeCount);
            AddTerminal("Published devices: " +
                DriverDiagnostics.DeviceCount);
            AddTerminal("Held resources: " +
                DriverDiagnostics.GetTotalHeldResourceCount());

            for (int i = 0; i < DriverDiagnostics.DriverCount; i++)
            {
                DriverInfo info = default(DriverInfo);

                if (DriverDiagnostics.TryGetDriver(i, out info))
                {
                    AddTerminal("  " + info.Name +
                        " (priority " + info.Priority + ")");
                }
            }

            for (int i = 0; i < DriverDiagnostics.DeviceCount; i++)
            {
                PublishedDeviceInfo info = default(PublishedDeviceInfo);

                if (DriverDiagnostics.TryGetDevice(i, out info))
                {
                    AddTerminal(
                        "  " + info.Kind + ": " + info.Name +
                        " -> " + info.DriverName
                    );
                }
            }
        }
        catch
        {
            AddTerminal("Driver diagnostics unavailable.");
        }
    }

    private void TerminalProcesses()
    {
        AddTerminal("PID  PROCESS");
        AddTerminal("  0  kernel");
        AddTerminal("  1  shell");

        for (int i = 0; i < windows.Count; i++)
        {
            AddTerminal(
                "  " + (i + 2) + "  " + GetAppName(windows[i].App) +
                (windows[i].Minimized ? " (minimized)" : "")
            );
        }
    }

    private void TerminalNeofetch()
    {
        AddTerminal("      ______");
        AddTerminal("   .-'      '-.");
        AddTerminal("  /  customOS  \\");
        AddTerminal(" |   v" + OS_VERSION + "      |");
        AddTerminal("  \\            /");
        AddTerminal("   '-.______.-'");
        AddTerminal("");

        try
        {
            AddTerminal("OS: customOS " + OS_VERSION + " x64");
            AddTerminal("Kernel: Cosmos Gen3");
            AddTerminal("CPU: " + GetCpuName());
            AddTerminal("Cores: " + Environment.ProcessorCount);
            AddTerminal("Memory: " +
                FormatBytes(MemoryDiagnostics.RamSizeBytes));
            AddTerminal("Storage: " +
                StorageManager.DeviceCount + " device(s)");
            AddTerminal("Network: " +
                NetworkManager.DeviceCount + " adapter(s)");
            AddTerminal("Resolution: " + screenWidth + "x" + screenHeight);
            AddTerminal("Uptime: " +
                FormatDuration(DateTime.Now - bootTime));
        }
        catch
        {
            AddTerminal("System details unavailable.");
        }
    }

    // ==========================================
    // FILE MANAGER APP
    // ==========================================

    private void DrawFileManager(Window window)
    {
        if (desktop == null)
            return;

        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        desktop.DrawFilledRectangle(WindowColor, x, y, width, height);

        int toolbarY = y + 5;

        DrawFileToolbarButton(x + 8, toolbarY, 44, 24, "Up");
        DrawFileToolbarButton(x + 56, toolbarY, 50, 24, "Home");
        DrawFileToolbarButton(x + 110, toolbarY, 64, 24, "Refresh");
        DrawFileToolbarButton(x + 178, toolbarY, 44, 24, "New");
        DrawFileToolbarButton(x + 226, toolbarY, 40, 24, "Del");
        DrawFileToolbarButton(x + 270, toolbarY, 48, 24, "Open");

        int pathY = y + 36;

        desktop.DrawFilledRectangle(
            PanelColor, x + 8, pathY, width - 16, 22
        );

        DrawTextClipped(
            filesDirectory,
            TextColor,
            x + 14, pathY + 4,
            width - 28
        );

        int headerY = y + 64;

        DrawText("Name", MutedTextColor, x + 36, headerY);
        DrawText(
            "Size",
            MutedTextColor,
            x + width - 150,
            headerY
        );
        DrawText(
            "Type",
            MutedTextColor,
            x + width - 92,
            headerY
        );

        int listY = y + 82;
        int statusY = y + height - 22;
        int rowHeight = 22;
        int maxRows = (statusY - listY - 4) / rowHeight;

        if (maxRows < 1)
            maxRows = 1;

        if (fileScroll > fileEntries.Count - maxRows)
            fileScroll = fileEntries.Count - maxRows;

        if (fileScroll < 0)
            fileScroll = 0;

        if (fileEntries.Count == 0)
        {
            DrawText(
                "This folder is empty.",
                MutedTextColor,
                x + 20,
                listY + 8
            );
        }

        for (int i = fileScroll;
             i < fileEntries.Count && i < fileScroll + maxRows;
             i++)
        {
            FileEntry entry = fileEntries[i];
            int rowY = listY + (i - fileScroll) * rowHeight;

            bool selected = i == fileSelected;
            bool hover = PointInRect(
                MouseManager.X, MouseManager.Y,
                x + 8, rowY, width - 16, rowHeight
            );

            if (selected)
            {
                desktop.DrawFilledRectangle(
                    Blend(Accent, WindowColor, 0.35f),
                    x + 8, rowY, width - 16, rowHeight
                );
            }
            else if (hover)
            {
                desktop.DrawFilledRectangle(
                    PanelAltColor,
                    x + 8, rowY, width - 16, rowHeight
                );
            }

            DrawIconTile(
                x + 12, rowY + 3, 16,
                entry.IsDirectory
                    ? Color.FromArgb(200, 150, 40)
                    : Color.FromArgb(60, 120, 200),
                entry.IsDirectory ? "D" : "F",
                false
            );

            DrawTextClipped(
                entry.Name,
                selected ? Color.White : TextColor,
                x + 36,
                rowY + 4,
                width - 200
            );

            string size = entry.IsDirectory
                ? "--"
                : FormatBytes((ulong)entry.Size);

            DrawText(
                size,
                selected ? Color.White : MutedTextColor,
                x + width - 146,
                rowY + 4
            );

            DrawText(
                entry.IsDirectory ? "Folder" : "File",
                selected ? Color.White : MutedTextColor,
                x + width - 90,
                rowY + 4
            );
        }

        desktop.DrawFilledRectangle(
            PanelColor, x, statusY, width, 22
        );

        string status =
            fileEntries.Count + " items  |  " + fileStatus;

        DrawTextClipped(
            status,
            MutedTextColor,
            x + 10,
            statusY + 4,
            width - 20
        );
    }

    private void DrawFileToolbarButton(
        int x, int y, int width, int height, string label)
    {
        if (desktop == null)
            return;

        bool hover = PointInRect(
            MouseManager.X, MouseManager.Y,
            x, y, width, height
        );

        desktop.DrawFilledRectangle(
            hover ? PanelAltColor : PanelColor,
            x, y, width, height
        );

        desktop.DrawRectangle(BorderColor, x, y, width, height);

        int textWidth = MeasureString(label);

        DrawText(
            label,
            TextColor,
            x + (width - textWidth) / 2,
            y + (height - 16) / 2
        );
    }

    private void RefreshFiles()
    {
        fileEntries.Clear();
        fileStatus = "";
        fileSelected = -1;
        fileScroll = 0;

        try
        {
            if (!Directory.Exists(filesDirectory))
            {
                fileStatus = "Directory not found";
                return;
            }

            string[] directories = Directory.GetDirectories(filesDirectory);
            string[] files = Directory.GetFiles(filesDirectory);

            for (int i = 0; i < directories.Length; i++)
            {
                FileEntry entry = new FileEntry();

                entry.Path = directories[i];
                entry.Name = GetNameFromPath(directories[i]);
                entry.IsDirectory = true;
                entry.Size = 0;

                fileEntries.Add(entry);
            }

            for (int i = 0; i < files.Length; i++)
            {
                FileEntry entry = new FileEntry();

                entry.Path = files[i];
                entry.Name = GetNameFromPath(files[i]);
                entry.IsDirectory = false;
                entry.Size = GetFileSize(files[i]);

                fileEntries.Add(entry);
            }

            SortFileEntries();

            fileStatus = "Ready";
        }
        catch
        {
            fileStatus = "Cannot read directory";
        }
    }

    private void SortFileEntries()
    {
        for (int i = 1; i < fileEntries.Count; i++)
        {
            FileEntry key = fileEntries[i];
            int j = i - 1;

            while (j >= 0 && CompareFileEntries(fileEntries[j], key) > 0)
            {
                fileEntries[j + 1] = fileEntries[j];
                j--;
            }

            fileEntries[j + 1] = key;
        }
    }

    private int CompareFileEntries(FileEntry first, FileEntry second)
    {
        if (first.IsDirectory != second.IsDirectory)
            return first.IsDirectory ? -1 : 1;

        return CompareNames(first.Name, second.Name);
    }

    private int CompareNames(string first, string second)
    {
        int length = Math.Min(first.Length, second.Length);

        for (int i = 0; i < length; i++)
        {
            char a = char.ToLowerInvariant(first[i]);
            char b = char.ToLowerInvariant(second[i]);

            if (a < b)
                return -1;

            if (a > b)
                return 1;
        }

        return first.Length - second.Length;
    }

    private void OpenFileEntry(int index)
    {
        if (index < 0 || index >= fileEntries.Count)
            return;

        FileEntry entry = fileEntries[index];

        if (entry.IsDirectory)
        {
            filesDirectory = entry.Path;
            RefreshFiles();
            return;
        }

        notepadPath = entry.Path;
        LoadNotepad();
        OpenApplication(APP_NOTEPAD);
    }

    private void DeleteFileEntry(int index)
    {
        if (index < 0 || index >= fileEntries.Count)
            return;

        FileEntry entry = fileEntries[index];
        string message = "";

        try
        {
            if (entry.IsDirectory)
            {
                try
                {
                    Directory.Delete(entry.Path, true);
                }
                catch
                {
                    Directory.Delete(entry.Path);
                }
            }
            else
            {
                File.Delete(entry.Path);
            }

            message = "Deleted " + entry.Name;
        }
        catch
        {
            message = "Could not delete " + entry.Name;
        }

        RefreshFiles();
        fileStatus = message;
    }

    private void CreateNewFolder()
    {
        try
        {
            string name = "New Folder";
            string path = JoinPath(filesDirectory, name);
            int number = 2;

            while (Directory.Exists(path))
            {
                name = "New Folder " + number;
                path = JoinPath(filesDirectory, name);
                number++;
            }

            Directory.CreateDirectory(path);

            string message = "Created " + name;
            RefreshFiles();
            fileStatus = message;
        }
        catch
        {
            fileStatus = "Could not create folder";
        }
    }

    private void GoUpFiles()
    {
        if (filesDirectory == "/" || filesDirectory.Length == 0)
            return;

        filesDirectory = GetParentPath(filesDirectory);
        RefreshFiles();
    }

    private void GoHomeFiles()
    {
        filesDirectory = "/mnt";
        RefreshFiles();
    }

    private void HandleFileManagerClick(
        Window window, int mx, int my)
    {
        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        int toolbarY = y + 5;

        if (PointInRect(mx, my, x + 8, toolbarY, 44, 24))
        {
            GoUpFiles();
            return;
        }

        if (PointInRect(mx, my, x + 56, toolbarY, 50, 24))
        {
            GoHomeFiles();
            return;
        }

        if (PointInRect(mx, my, x + 110, toolbarY, 64, 24))
        {
            RefreshFiles();
            return;
        }

        if (PointInRect(mx, my, x + 178, toolbarY, 44, 24))
        {
            CreateNewFolder();
            return;
        }

        if (PointInRect(mx, my, x + 226, toolbarY, 40, 24))
        {
            DeleteFileEntry(fileSelected);
            return;
        }

        if (PointInRect(mx, my, x + 270, toolbarY, 48, 24))
        {
            OpenFileEntry(fileSelected);
            return;
        }

        int listY = y + 82;
        int statusY = y + height - 22;
        int rowHeight = 22;
        int maxRows = (statusY - listY - 4) / rowHeight;

        if (maxRows < 1)
            maxRows = 1;

        for (int i = fileScroll;
             i < fileEntries.Count && i < fileScroll + maxRows;
             i++)
        {
            int rowY = listY + (i - fileScroll) * rowHeight;

            if (PointInRect(mx, my, x + 8, rowY, width - 16, rowHeight))
            {
                long now = DateTime.Now.Ticks;

                bool doubleClick =
                    i == lastFileClickIndex &&
                    now - lastFileClickTicks < 5000000;

                fileSelected = i;
                lastFileClickIndex = i;
                lastFileClickTicks = now;

                if (doubleClick)
                    OpenFileEntry(i);

                return;
            }
        }
    }

    private void HandleFilesKey(KeyEvent key)
    {
        if (key.Key == Key.UpArrow)
        {
            if (fileEntries.Count > 0)
            {
                fileSelected--;

                if (fileSelected < 0)
                    fileSelected = 0;

                EnsureFileSelectionVisible();
            }

            return;
        }

        if (key.Key == Key.DownArrow)
        {
            if (fileEntries.Count > 0)
            {
                fileSelected++;

                if (fileSelected >= fileEntries.Count)
                    fileSelected = fileEntries.Count - 1;

                EnsureFileSelectionVisible();
            }

            return;
        }

        if (key.Key == Key.Enter)
        {
            OpenFileEntry(fileSelected);
            return;
        }

        if (key.Key == Key.Backspace)
        {
            GoUpFiles();
            return;
        }

        if (key.Key == Key.Delete)
        {
            DeleteFileEntry(fileSelected);
            return;
        }

        if (key.Key == Key.F5)
        {
            RefreshFiles();
            return;
        }

        if (key.Key == Key.F2)
        {
            CreateNewFolder();
            return;
        }

        if (key.Key == Key.Home)
        {
            GoHomeFiles();
        }
    }

    private void EnsureFileSelectionVisible()
    {
        Window? window = FindWindow(APP_FILES);

        if (window == null)
            return;

        int height = window.Height - TITLEBAR_HEIGHT - 1;
        int maxRows = (height - 82 - 26) / 22;

        if (maxRows < 1)
            maxRows = 1;

        if (fileSelected < fileScroll)
            fileScroll = fileSelected;

        if (fileSelected >= fileScroll + maxRows)
            fileScroll = fileSelected - maxRows + 1;

        if (fileScroll < 0)
            fileScroll = 0;
    }

    // ==========================================
    // SYSTEM MONITOR APP
    // ==========================================

    private void DrawSystemMonitor(Window window)
    {
        if (desktop == null)
            return;

        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        desktop.DrawFilledRectangle(WindowColor, x, y, width, height);

        int tabWidth = width / systemTabNames.Length;

        for (int i = 0; i < systemTabNames.Length; i++)
        {
            int tabX = x + i * tabWidth;
            int actualWidth = (i == systemTabNames.Length - 1)
                ? width - i * tabWidth
                : tabWidth;

            bool active = i == systemTab;
            bool hover = PointInRect(
                MouseManager.X, MouseManager.Y,
                tabX, y, actualWidth, 28
            );

            desktop.DrawFilledRectangle(
                active
                    ? Accent
                    : (hover ? PanelAltColor : PanelColor),
                tabX, y, actualWidth, 28
            );

            int textWidth = MeasureString(systemTabNames[i]);

            DrawText(
                systemTabNames[i],
                active ? Color.White : TextColor,
                tabX + (actualWidth - textWidth) / 2,
                y + 6
            );
        }

        if (DateTime.Now.Ticks - lastSystemRefresh >
            TimeSpan.TicksPerSecond)
        {
            RefreshSystemTab();
        }

        int contentY = y + 38;
        int lineHeight = 18;
        int maxLines = (height - 60) / lineHeight;

        if (maxLines < 1)
            maxLines = 1;

        for (int i = 0; i < systemLines.Count && i < maxLines; i++)
        {
            string line = systemLines[i];
            Color color = TextColor;

            if (line.StartsWith("!"))
            {
                color = Color.FromArgb(235, 150, 70);
                line = line.Substring(1);
            }
            else if (line.StartsWith("-"))
            {
                color = MutedTextColor;
                line = line.Substring(1);
            }
            else if (line.StartsWith("+"))
            {
                color = Accent;
                line = line.Substring(1);
            }

            DrawTextClipped(
                line,
                color,
                x + 14,
                contentY + i * lineHeight,
                width - 28
            );
        }

        desktop.DrawFilledRectangle(
            PanelColor, x, y + height - 20, width, 20
        );

        DrawText(
            "F5 refresh   Left/Right switch tabs",
            MutedTextColor,
            x + 10,
            y + height - 18
        );
    }

    private void RefreshSystemTab()
    {
        systemLines.Clear();

        if (systemTab == 0)
            RefreshOverviewTab();
        else if (systemTab == 1)
            RefreshMemoryTab();
        else if (systemTab == 2)
            RefreshStorageTab();
        else if (systemTab == 3)
            RefreshNetworkTab();
        else
            RefreshDriversTab();

        lastSystemRefresh = DateTime.Now.Ticks;
    }

    private void RefreshOverviewTab()
    {
        AddSystemLine("+SYSTEM OVERVIEW");
        AddSystemLine("Operating system: " + OS_NAME + " " + OS_VERSION);
        AddSystemLine("Kernel: Cosmos Gen3");
        AddSystemLine("Architecture: " +
            RuntimeInformation.ProcessArchitecture);
        AddSystemLine("CPU: " + GetCpuName());
        AddSystemLine("Logical processors: " + Environment.ProcessorCount);
        AddSystemLine("Uptime: " +
            FormatDuration(DateTime.Now - bootTime));
        AddSystemLine("Resolution: " + screenWidth + "x" + screenHeight);

        try
        {
            if (DisplayManager.IsEnabled && DisplayManager.Count > 0)
            {
                DisplayDevice display = DisplayManager.Primary;

                AddSystemLine("Display: " + display.Name);
                AddSystemLine("Driver: " + display.DriverName);
                AddSystemLine("Mode: " + display.Width + "x" +
                    display.Height + " @ " + display.RefreshRate + " Hz");
                AddSystemLine("Color depth: " +
                    display.BitsPerPixel + " bpp");
            }
        }
        catch
        {
            AddSystemLine("-Display details unavailable.");
        }

        try
        {
            AddSystemLine("Storage devices: " + StorageManager.DeviceCount);
            AddSystemLine("Network adapters: " + NetworkManager.DeviceCount);
        }
        catch
        {
            AddSystemLine("-Device information unavailable.");
        }

        try
        {
            AddSystemLine("Drivers: " + DriverDiagnostics.DriverCount +
                "   Devices: " + DriverDiagnostics.DeviceCount);
            AddSystemLine("Device nodes: " + DriverDiagnostics.NodeCount);
        }
        catch
        {
            AddSystemLine("-Driver diagnostics unavailable.");
        }

        AddSystemLine("Managed heap: " +
            FormatBytes((ulong)GC.GetTotalMemory(false)));
    }

    private void RefreshMemoryTab()
    {
        AddSystemLine("+MEMORY");

        try
        {
            ulong ram = MemoryDiagnostics.RamSizeBytes;
            ulong pageSize = MemoryDiagnostics.PageSizeBytes;
            ulong totalPages = MemoryDiagnostics.TotalPages;
            ulong freePages = MemoryDiagnostics.FreePages;
            ulong usedPages = totalPages - freePages;
            int percent = totalPages > 0
                ? (int)(usedPages * 100 / totalPages)
                : 0;

            AddSystemLine("Physical RAM: " + FormatBytes(ram));
            AddSystemLine("Page size: " + pageSize + " bytes");
            AddSystemLine("Total pages: " + totalPages);
            AddSystemLine("Used pages: " + usedPages);
            AddSystemLine("Free pages: " + freePages);
            AddSystemLine("Usage: " + MakeBar(percent, 30) +
                " " + percent + "%");
            AddSystemLine("Managed heap: " +
                FormatBytes((ulong)GC.GetTotalMemory(false)));
            AddSystemLine("GC time: " +
                MemoryDiagnostics.GcTimePercent + "%");
            AddSystemLine("Collections: " +
                MemoryDiagnostics.TotalCollections);
            AddSystemLine("Objects freed: " +
                MemoryDiagnostics.TotalObjectsFreed);
        }
        catch
        {
            AddSystemLine("!Memory diagnostics unavailable.");
        }
    }

    private void RefreshStorageTab()
    {
        AddSystemLine("+STORAGE");

        try
        {
            if (!StorageManager.IsEnabled)
            {
                AddSystemLine("!Storage subsystem disabled.");
                return;
            }

            AddSystemLine("Devices: " + StorageManager.DeviceCount);

            for (int i = 0; i < StorageManager.DeviceCount; i++)
            {
                IBlockDevice device = StorageManager.GetDevice(i);

                AddSystemLine("[" + i + "] " + device.Name);
                AddSystemLine("    " +
                    FormatBytes(device.BlockCount * device.BlockSize) +
                    "   " + device.BlockCount + " blocks x " +
                    device.BlockSize + " bytes");
            }

            IReadOnlyList<Partition> partitions = StorageManager.Partitions;

            AddSystemLine("Partitions: " + partitions.Count);

            for (int i = 0; i < partitions.Count; i++)
            {
                Partition partition = partitions[i];

                AddSystemLine("    " + partition.Name + "   " +
                    FormatBytes(partition.BlockCount * partition.BlockSize));
            }

            IReadOnlyList<VfsMount> mounts = VfsManager.Mounts;

            AddSystemLine("Mounts: " + mounts.Count);

            for (int i = 0; i < mounts.Count; i++)
            {
                VfsMount mount = mounts[i];

                AddSystemLine("    " + mount.MountPoint +
                    " <- " + mount.Source +
                    (mount.IsReadOnly ? " (ro)" : " (rw)"));

                VfsStatFs stat = default(VfsStatFs);

                if (VfsManager.TryStatFs(mount.MountPoint, out stat) &&
                    stat.BlockSize > 0)
                {
                    ulong total = stat.BlockSize * stat.Blocks;
                    ulong free = stat.BlockSize * stat.FreeBlocks;
                    ulong used = total - free;
                    int percent = total > 0
                        ? (int)(used * 100 / total)
                        : 0;

                    AddSystemLine("      " + MakeBar(percent, 20) +
                        " " + FormatBytes(used) + " / " +
                        FormatBytes(total));
                }
            }
        }
        catch
        {
            AddSystemLine("!Storage information unavailable.");
        }
    }

    private void RefreshNetworkTab()
    {
        AddSystemLine("+NETWORK");

        try
        {
            if (!NetworkManager.IsEnabled)
            {
                AddSystemLine("!Network subsystem disabled.");
                return;
            }

            AddSystemLine("Adapters: " + NetworkManager.DeviceCount);

            for (int i = 0; i < NetworkManager.DeviceCount; i++)
            {
                NetworkAdapter adapter = NetworkManager.GetAdapter(i);

                AddSystemLine("[" + i + "] " + adapter.Name);
                AddSystemLine("    MAC: " + adapter.MacAddress.ToString());
                AddSystemLine("    Link: " +
                    (adapter.LinkUp ? "up" : "down") +
                    "   Ready: " + adapter.Ready);

                IPConfig? config = adapter.IPConfig;

                if (config != null)
                {
                    AddSystemLine("    IP: " +
                        config.Address.ToString());
                    AddSystemLine("    Mask: " +
                        config.SubnetMask.ToString());
                    AddSystemLine("    Gateway: " +
                        config.DefaultGateway.ToString());
                }
            }

            AddSystemLine("Hostname: " + DnsConfig.HostName);

            IReadOnlyList<Address> dns = DnsConfig.Nameservers;

            for (int i = 0; i < dns.Count; i++)
                AddSystemLine("DNS " + (i + 1) + ": " + dns[i].ToString());
        }
        catch
        {
            AddSystemLine("!Network information unavailable.");
        }
    }

    private void RefreshDriversTab()
    {
        AddSystemLine("+DRIVERS");

        try
        {
            if (!DriverDiagnostics.IsStarted)
            {
                AddSystemLine("!Driver diagnostics not started.");
                return;
            }

            AddSystemLine("Started: " + DriverDiagnostics.IsStarted);
            AddSystemLine("Worker: " + DriverDiagnostics.HasWorker);
            AddSystemLine("Drivers: " + DriverDiagnostics.DriverCount);
            AddSystemLine("Device nodes: " + DriverDiagnostics.NodeCount);
            AddSystemLine("Published devices: " +
                DriverDiagnostics.DeviceCount);
            AddSystemLine("Held resources: " +
                DriverDiagnostics.GetTotalHeldResourceCount());
            AddSystemLine("");

            for (int i = 0; i < DriverDiagnostics.DriverCount; i++)
            {
                DriverInfo info = default(DriverInfo);

                if (DriverDiagnostics.TryGetDriver(i, out info))
                {
                    AddSystemLine("Driver: " + info.Name +
                        " (priority " + info.Priority + ")");
                }
            }

            AddSystemLine("");

            for (int i = 0; i < DriverDiagnostics.DeviceCount; i++)
            {
                PublishedDeviceInfo info = default(PublishedDeviceInfo);

                if (DriverDiagnostics.TryGetDevice(i, out info))
                {
                    AddSystemLine("Device: " + info.Kind + " " +
                        info.Name);
                    AddSystemLine("    Driver: " + info.DriverName +
                        "   Consumed: " + info.IsConsumed);
                }
            }
        }
        catch
        {
            AddSystemLine("!Driver diagnostics unavailable.");
        }
    }

    private void AddSystemLine(string line)
    {
        systemLines.Add(line);
    }

    private void HandleSystemMonitorClick(
        Window window, int mx, int my)
    {
        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;

        int tabWidth = width / systemTabNames.Length;

        if (my < y || my > y + 28)
            return;

        for (int i = 0; i < systemTabNames.Length; i++)
        {
            int tabX = x + i * tabWidth;
            int actualWidth = (i == systemTabNames.Length - 1)
                ? width - i * tabWidth
                : tabWidth;

            if (PointInRect(mx, my, tabX, y, actualWidth, 28))
            {
                systemTab = i;
                lastSystemRefresh = 0;
                return;
            }
        }
    }

    private void HandleSystemKey(KeyEvent key)
    {
        if (key.Key == Key.LeftArrow)
        {
            systemTab--;

            if (systemTab < 0)
                systemTab = systemTabNames.Length - 1;

            lastSystemRefresh = 0;
            return;
        }

        if (key.Key == Key.RightArrow)
        {
            systemTab++;

            if (systemTab >= systemTabNames.Length)
                systemTab = 0;

            lastSystemRefresh = 0;
            return;
        }

        if (key.Key == Key.D1)
            SelectSystemTab(0);
        else if (key.Key == Key.D2)
            SelectSystemTab(1);
        else if (key.Key == Key.D3)
            SelectSystemTab(2);
        else if (key.Key == Key.D4)
            SelectSystemTab(3);
        else if (key.Key == Key.D5)
            SelectSystemTab(4);
    }

    private void SelectSystemTab(int index)
    {
        systemTab = index;
        lastSystemRefresh = 0;
    }

    // ==========================================
    // SETTINGS APP
    // ==========================================

    private void DrawSettings(Window window)
    {
        if (desktop == null)
            return;

        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        desktop.DrawFilledRectangle(WindowColor, x, y, width, height);

        DrawText("PERSONALIZE CUSTOMOS", Accent, x + 20, y + 12);
        DrawText(
            "Changes are saved to /mnt/settings.cfg",
            MutedTextColor,
            x + 20, y + 30
        );

        DrawText("Theme", TextColor, x + 20, y + 58);

        DrawSettingsButton(
            x + 250, y + 50, 130, 28,
            darkTheme ? "Dark" : "Light"
        );

        DrawText("Accent color", TextColor, x + 20, y + 98);

        for (int i = 0; i < accentPalette.Length; i++)
        {
            int swatchX = x + 250 + i * 32;

            desktop.DrawFilledRectangle(
                accentPalette[i], swatchX, y + 90, 24, 24
            );

            desktop.DrawRectangle(
                i == accentIndex ? Color.White : BorderColor,
                swatchX, y + 90, 24, 24
            );
        }

        DrawText("Wallpaper", TextColor, x + 20, y + 138);

        DrawSettingsButton(x + 250, y + 130, 28, 28, "<");
        DrawSettingsButton(x + 420, y + 130, 28, 28, ">");

        DrawText(
            GetWallpaperName(),
            TextColor,
            x + 286,
            y + 136
        );

        DrawText("Clock seconds", TextColor, x + 20, y + 178);

        DrawSettingsButton(
            x + 250, y + 170, 130, 28,
            showSeconds ? "On" : "Off"
        );

        DrawText("Mouse sensitivity", TextColor, x + 20, y + 218);

        DrawSettingsButton(x + 250, y + 210, 28, 28, "-");
        DrawSettingsButton(x + 340, y + 210, 28, 28, "+");

        DrawText(
            mouseSensitivity.ToString("F1"),
            TextColor,
            x + 286,
            y + 216
        );

        DrawSettingsButton(x + 20, y + 266, 140, 32, "Save settings");
        DrawSettingsButton(x + 170, y + 266, 140, 32, "Reset defaults");

        desktop.DrawFilledRectangle(
            PanelColor,
            x + 20, y + 318, width - 40, 26
        );

        DrawTextClipped(
            settingsStatus.Length > 0
                ? settingsStatus
                : "Select an option above.",
            MutedTextColor,
            x + 28,
            y + 324,
            width - 56
        );

        DrawText(
            "Shortcuts: T theme  W wallpaper  A accent  +/- sensitivity",
            MutedTextColor,
            x + 20,
            y + height - 24
        );
    }

    private void DrawSettingsButton(
        int x, int y, int width, int height, string label)
    {
        if (desktop == null)
            return;

        bool hover = PointInRect(
            MouseManager.X, MouseManager.Y,
            x, y, width, height
        );

        desktop.DrawFilledRectangle(
            hover ? Accent : PanelAltColor,
            x, y, width, height
        );

        desktop.DrawRectangle(
            hover ? Color.White : BorderColor,
            x, y, width, height
        );

        int textWidth = MeasureString(label);

        DrawText(
            label,
            Color.White,
            x + (width - textWidth) / 2,
            y + (height - 16) / 2
        );
    }

    private void LoadSettings()
    {
        darkTheme = true;
        accentIndex = 0;
        wallpaperStyle = 0;
        showSeconds = true;
        mouseSensitivity = 1.0f;

        try
        {
            if (!File.Exists("/mnt/settings.cfg"))
                return;

            string text = File.ReadAllText("/mnt/settings.cfg");
            string[] lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Replace("\r", "").Trim();

                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                int separator = line.IndexOf('=');

                if (separator <= 0)
                    continue;

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();

                if (key == "theme")
                {
                    darkTheme = value != "light";
                }
                else if (key == "accent")
                {
                    int number;

                    if (int.TryParse(value, out number))
                        accentIndex = number;
                }
                else if (key == "wallpaper")
                {
                    int number;

                    if (int.TryParse(value, out number))
                        wallpaperStyle = number;
                }
                else if (key == "seconds")
                {
                    showSeconds = value != "0" && value != "false";
                }
                else if (key == "sensitivity")
                {
                    double number;

                    if (double.TryParse(value, out number))
                        mouseSensitivity = (float)number;
                }
            }
        }
        catch
        {
        }

        ApplySettings();
    }

    private void SaveSettings()
    {
        try
        {
            StringBuilder builder = new StringBuilder();

            builder.Append("theme=");
            builder.Append(darkTheme ? "dark" : "light");
            builder.Append("\n");

            builder.Append("accent=");
            builder.Append(accentIndex);
            builder.Append("\n");

            builder.Append("wallpaper=");
            builder.Append(wallpaperStyle);
            builder.Append("\n");

            builder.Append("seconds=");
            builder.Append(showSeconds ? "1" : "0");
            builder.Append("\n");

            builder.Append("sensitivity=");
            builder.Append(mouseSensitivity.ToString("F1"));
            builder.Append("\n");

            File.WriteAllText("/mnt/settings.cfg", builder.ToString());
        }
        catch
        {
        }
    }

    private void ApplySettings()
    {
        if (accentIndex < 0)
            accentIndex = 0;

        if (accentIndex >= accentPalette.Length)
            accentIndex = accentPalette.Length - 1;

        if (wallpaperStyle < 0)
            wallpaperStyle = 0;

        if (wallpaperStyle > 4)
            wallpaperStyle = 4;

        if (mouseSensitivity < 0.5f)
            mouseSensitivity = 0.5f;

        if (mouseSensitivity > 3.0f)
            mouseSensitivity = 3.0f;

        try
        {
            MouseManager.Sensitivity = mouseSensitivity;
        }
        catch
        {
        }
    }

    private void ResetSettings()
    {
        darkTheme = true;
        accentIndex = 0;
        wallpaperStyle = 0;
        showSeconds = true;
        mouseSensitivity = 1.0f;

        ApplySettings();
        SaveSettings();

        settingsStatus = "Defaults restored.";
    }

    private string GetWallpaperName()
    {
        switch (wallpaperStyle)
        {
            case 1: return "Midnight";
            case 2: return "Forest";
            case 3: return "Sunset";
            case 4: return "Solid accent";
            default: return "Aurora";
        }
    }

    private void HandleSettingsClick(
        Window window, int mx, int my)
    {
        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;

        if (PointInRect(mx, my, x + 250, y + 50, 130, 28))
        {
            darkTheme = !darkTheme;
            ApplySettings();
            SaveSettings();
            settingsStatus = "Theme changed.";
            return;
        }

        for (int i = 0; i < accentPalette.Length; i++)
        {
            if (PointInRect(
                mx, my,
                x + 250 + i * 32, y + 90, 24, 24))
            {
                accentIndex = i;
                ApplySettings();
                SaveSettings();
                settingsStatus = "Accent changed.";
                return;
            }
        }

        if (PointInRect(mx, my, x + 250, y + 130, 28, 28))
        {
            wallpaperStyle--;

            if (wallpaperStyle < 0)
                wallpaperStyle = 4;

            ApplySettings();
            SaveSettings();
            settingsStatus = "Wallpaper changed.";
            return;
        }

        if (PointInRect(mx, my, x + 420, y + 130, 28, 28))
        {
            wallpaperStyle = (wallpaperStyle + 1) % 5;
            ApplySettings();
            SaveSettings();
            settingsStatus = "Wallpaper changed.";
            return;
        }

        if (PointInRect(mx, my, x + 250, y + 170, 130, 28))
        {
            showSeconds = !showSeconds;
            SaveSettings();
            settingsStatus = "Clock setting changed.";
            return;
        }

        if (PointInRect(mx, my, x + 250, y + 210, 28, 28))
        {
            mouseSensitivity -= 0.1f;
            ApplySettings();
            SaveSettings();
            settingsStatus = "Mouse sensitivity changed.";
            return;
        }

        if (PointInRect(mx, my, x + 340, y + 210, 28, 28))
        {
            mouseSensitivity += 0.1f;
            ApplySettings();
            SaveSettings();
            settingsStatus = "Mouse sensitivity changed.";
            return;
        }

        if (PointInRect(mx, my, x + 20, y + 266, 140, 32))
        {
            SaveSettings();
            settingsStatus = "Settings saved.";
            return;
        }

        if (PointInRect(mx, my, x + 170, y + 266, 140, 32))
        {
            ResetSettings();
        }
    }

    private void HandleSettingsKey(KeyEvent key)
    {
        if (key.Key == Key.T)
        {
            darkTheme = !darkTheme;
            ApplySettings();
            SaveSettings();
            settingsStatus = "Theme changed.";
        }
        else if (key.Key == Key.W)
        {
            wallpaperStyle = (wallpaperStyle + 1) % 5;
            ApplySettings();
            SaveSettings();
            settingsStatus = "Wallpaper changed.";
        }
        else if (key.Key == Key.A)
        {
            accentIndex = (accentIndex + 1) % accentPalette.Length;
            ApplySettings();
            SaveSettings();
            settingsStatus = "Accent changed.";
        }
        else if (key.Key == Key.Equal || key.Key == Key.NumPlus)
        {
            mouseSensitivity += 0.1f;
            ApplySettings();
            SaveSettings();
            settingsStatus = "Mouse sensitivity changed.";
        }
        else if (key.Key == Key.Minus || key.Key == Key.NumMinus)
        {
            mouseSensitivity -= 0.1f;
            ApplySettings();
            SaveSettings();
            settingsStatus = "Mouse sensitivity changed.";
        }
        else if (key.Key == Key.S)
        {
            SaveSettings();
            settingsStatus = "Settings saved.";
        }
    }

    // ==========================================
    // NOTEPAD APP
    // ==========================================

    private void DrawNotepad(Window window)
    {
        if (desktop == null)
            return;

        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        desktop.DrawFilledRectangle(WindowColor, x, y, width, height);

        DrawSettingsButton(x + 8, y + 5, 70, 24, "Save");
        DrawSettingsButton(x + 84, y + 5, 80, 24, "Reload");
        DrawSettingsButton(x + 170, y + 5, 70, 24, "Clear");

        DrawTextClipped(
            "File: " + notepadPath,
            MutedTextColor,
            x + 252,
            y + 9,
            width - 262
        );

        int editorX = x + 8;
        int editorY = y + 36;
        int editorWidth = width - 16;
        int editorHeight = height - 66;

        desktop.DrawFilledRectangle(
            darkTheme
                ? Color.FromArgb(18, 24, 34)
                : Color.FromArgb(255, 255, 255),
            editorX, editorY, editorWidth, editorHeight
        );

        desktop.DrawRectangle(
            BorderColor,
            editorX, editorY, editorWidth, editorHeight
        );

        int lineHeight = 16;
        int textX = editorX + 36;
        int textY = editorY + 6;
        int maxLines = (editorHeight - 12) / lineHeight;

        if (maxLines < 1)
            maxLines = 1;

        string[] lines = notepadText.Split('\n');

        if (notepadScrollLine > lines.Length - maxLines)
            notepadScrollLine = lines.Length - maxLines;

        if (notepadScrollLine < 0)
            notepadScrollLine = 0;

        for (int i = 0; i < maxLines; i++)
        {
            int lineIndex = notepadScrollLine + i;

            if (lineIndex >= lines.Length)
                break;

            string lineNumber = (lineIndex + 1).ToString();

            DrawText(
                lineNumber,
                MutedTextColor,
                editorX + 8,
                textY + i * lineHeight
            );

            DrawTextClipped(
                lines[lineIndex].Replace("\r", ""),
                TextColor,
                textX,
                textY + i * lineHeight,
                editorWidth - 44
            );
        }

        if (notepadText.Length == 0)
        {
            DrawText(
                "Start typing to edit this file.",
                MutedTextColor,
                textX,
                textY
            );
        }

        int caretLine;
        int caretColumn;
        GetLineColumn(notepadText, notepadCaret,
            out caretLine, out caretColumn);

        if (caretLine >= notepadScrollLine &&
            caretLine < notepadScrollLine + maxLines)
        {
            int caretX = textX + caretColumn * 8;
            int caretY = textY +
                (caretLine - notepadScrollLine) * lineHeight;

            desktop.DrawFilledRectangle(
                Accent, caretX, caretY, 2, lineHeight
            );
        }

        DrawTextClipped(
            "Ln " + (caretLine + 1) + ", Col " + (caretColumn + 1) +
            "  |  " + notepadStatus +
            "  |  Ctrl+S save  Ctrl+R reload",
            MutedTextColor,
            x + 10,
            y + height - 18,
            width - 20
        );
    }

    private void LoadNotepad()
    {
        notepadStatus = "";
        notepadCaret = 0;
        notepadScrollLine = 0;

        try
        {
            if (File.Exists(notepadPath))
            {
                notepadText = File.ReadAllText(notepadPath);
                notepadStatus = "Loaded " + GetNameFromPath(notepadPath);
            }
            else
            {
                notepadText = "";
                notepadStatus = "New file";
            }
        }
        catch
        {
            notepadText = "";
            notepadStatus = "Load failed";
        }

        notepadCaret = notepadText.Length;
    }

    private void SaveNotepad()
    {
        try
        {
            File.WriteAllText(notepadPath, notepadText);
            notepadStatus = "Saved " + GetNameFromPath(notepadPath);
        }
        catch
        {
            notepadStatus = "Save failed";
        }
    }

    private void ClearNotepad()
    {
        notepadText = "";
        notepadCaret = 0;
        notepadScrollLine = 0;
        notepadStatus = "Cleared";
    }

    private void InsertNotepad(string text)
    {
        if (notepadCaret < 0)
            notepadCaret = 0;

        if (notepadCaret > notepadText.Length)
            notepadCaret = notepadText.Length;

        notepadText =
            notepadText.Substring(0, notepadCaret) +
            text +
            notepadText.Substring(notepadCaret);

        notepadCaret += text.Length;
        notepadStatus = "Modified";
        EnsureNotepadCaretVisible();
    }

    private void BackspaceNotepad()
    {
        if (notepadCaret <= 0)
            return;

        notepadText =
            notepadText.Substring(0, notepadCaret - 1) +
            notepadText.Substring(notepadCaret);

        notepadCaret--;
        notepadStatus = "Modified";
        EnsureNotepadCaretVisible();
    }

    private void DeleteNotepad()
    {
        if (notepadCaret >= notepadText.Length)
            return;

        notepadText =
            notepadText.Substring(0, notepadCaret) +
            notepadText.Substring(notepadCaret + 1);

        notepadStatus = "Modified";
        EnsureNotepadCaretVisible();
    }

    private void MoveNotepadCaret(int amount)
    {
        notepadCaret += amount;

        if (notepadCaret < 0)
            notepadCaret = 0;

        if (notepadCaret > notepadText.Length)
            notepadCaret = notepadText.Length;

        EnsureNotepadCaretVisible();
    }

    private void MoveNotepadLine(int direction)
    {
        int line;
        int column;
        GetLineColumn(notepadText, notepadCaret, out line, out column);

        line += direction;

        if (line < 0)
            line = 0;

        int lineCount = CountNotepadLines();

        if (line >= lineCount)
            line = lineCount - 1;

        notepadCaret = GetCaretFromLineColumn(
            notepadText, line, column
        );

        EnsureNotepadCaretVisible();
    }

    private void MoveNotepadHome()
    {
        int line;
        int column;
        GetLineColumn(notepadText, notepadCaret, out line, out column);

        notepadCaret = GetCaretFromLineColumn(
            notepadText, line, 0
        );

        EnsureNotepadCaretVisible();
    }

    private void MoveNotepadEnd()
    {
        int line;
        int column;
        GetLineColumn(notepadText, notepadCaret, out line, out column);

        notepadCaret = GetCaretFromLineColumn(
            notepadText, line, GetNotepadLineLength(line)
        );

        EnsureNotepadCaretVisible();
    }

    private int CountNotepadLines()
    {
        int count = 1;

        for (int i = 0; i < notepadText.Length; i++)
        {
            if (notepadText[i] == '\n')
                count++;
        }

        return count;
    }

    private int GetNotepadLineLength(int line)
    {
        int current = 0;
        int start = 0;

        for (int i = 0; i <= notepadText.Length; i++)
        {
            if (i == notepadText.Length || notepadText[i] == '\n')
            {
                if (current == line)
                {
                    int end = i;

                    if (end > start && notepadText[end - 1] == '\r')
                        end--;

                    return end - start;
                }

                current++;
                start = i + 1;
            }
        }

        return 0;
    }

    private void GetLineColumn(
        string text, int caret, out int line, out int column)
    {
        line = 0;
        column = 0;

        if (caret < 0)
            caret = 0;

        if (caret > text.Length)
            caret = text.Length;

        for (int i = 0; i < caret; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 0;
            }
            else
            {
                column++;
            }
        }
    }

    private int GetCaretFromLineColumn(
        string text, int targetLine, int targetColumn)
    {
        int line = 0;
        int column = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (line == targetLine && column >= targetColumn)
                return i;

            if (text[i] == '\n')
            {
                if (line == targetLine)
                    return i;

                line++;
                column = 0;
            }
            else
            {
                column++;
            }
        }

        return text.Length;
    }

    private void EnsureNotepadCaretVisible()
    {
        int line;
        int column;
        GetLineColumn(notepadText, notepadCaret, out line, out column);

        Window? window = FindWindow(APP_NOTEPAD);
        int maxLines = 16;

        if (window != null)
        {
            int height = window.Height - TITLEBAR_HEIGHT - 1;
            maxLines = (height - 66 - 12) / 16;
        }

        if (maxLines < 1)
            maxLines = 1;

        if (line < notepadScrollLine)
            notepadScrollLine = line;

        if (line >= notepadScrollLine + maxLines)
            notepadScrollLine = line - maxLines + 1;

        if (notepadScrollLine < 0)
            notepadScrollLine = 0;
    }

    private void HandleNotepadKey(
        KeyEvent key, bool control, bool shift)
    {
        if (control && key.Key == Key.S)
        {
            SaveNotepad();
            return;
        }

        if (control && key.Key == Key.R)
        {
            LoadNotepad();
            return;
        }

        if (control && key.Key == Key.L)
        {
            ClearNotepad();
            return;
        }

        if (key.Key == Key.Enter)
        {
            InsertNotepad("\n");
            return;
        }

        if (key.Key == Key.Backspace)
        {
            BackspaceNotepad();
            return;
        }

        if (key.Key == Key.Delete)
        {
            DeleteNotepad();
            return;
        }

        if (key.Key == Key.LeftArrow)
        {
            MoveNotepadCaret(-1);
            return;
        }

        if (key.Key == Key.RightArrow)
        {
            MoveNotepadCaret(1);
            return;
        }

        if (key.Key == Key.UpArrow)
        {
            MoveNotepadLine(-1);
            return;
        }

        if (key.Key == Key.DownArrow)
        {
            MoveNotepadLine(1);
            return;
        }

        if (key.Key == Key.Home)
        {
            MoveNotepadHome();
            return;
        }

        if (key.Key == Key.End)
        {
            MoveNotepadEnd();
            return;
        }

        if (key.Key == Key.PageUp)
        {
            MoveNotepadLine(-10);
            return;
        }

        if (key.Key == Key.PageDown)
        {
            MoveNotepadLine(10);
            return;
        }

        if (key.Key == Key.Tab)
        {
            InsertNotepad("    ");
            return;
        }

        char character = key.KeyChar;

        if (character >= 32 && character <= 126)
            InsertNotepad(character.ToString());
    }

    private void HandleNotepadClick(
        Window window, int mx, int my)
    {
        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;

        if (PointInRect(mx, my, x + 8, y + 5, 70, 24))
        {
            SaveNotepad();
            return;
        }

        if (PointInRect(mx, my, x + 84, y + 5, 80, 24))
        {
            LoadNotepad();
            return;
        }

        if (PointInRect(mx, my, x + 170, y + 5, 70, 24))
        {
            ClearNotepad();
            return;
        }

        int editorX = x + 8;
        int editorY = y + 36;
        int editorWidth = window.Width - 18;
        int editorHeight = window.Height - TITLEBAR_HEIGHT - 67;

        if (!PointInRect(
            mx, my, editorX, editorY, editorWidth, editorHeight))
        {
            return;
        }

        int line = (my - (editorY + 6)) / 16 + notepadScrollLine;
        int column = (mx - (editorX + 36)) / 8;

        if (line < 0)
            line = 0;

        if (column < 0)
            column = 0;

        int lineCount = CountNotepadLines();

        if (line >= lineCount)
            line = lineCount - 1;

        int lineLength = GetNotepadLineLength(line);

        if (column > lineLength)
            column = lineLength;

        notepadCaret = GetCaretFromLineColumn(
            notepadText, line, column
        );

        EnsureNotepadCaretVisible();
    }

    // ==========================================
    // CALCULATOR APP
    // ==========================================

    private static readonly string[] calcLabels = new string[]
    {
        "C", "DEL", "/", "*",
        "7", "8", "9", "-",
        "4", "5", "6", "+",
        "1", "2", "3", "=",
        "0", ".", "(", ")"
    };

    private void DrawCalculator(Window window)
    {
        if (desktop == null)
            return;

        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        desktop.DrawFilledRectangle(WindowColor, x, y, width, height);

        desktop.DrawFilledRectangle(
            PanelColor, x + 10, y + 10, width - 20, 92
        );

        desktop.DrawRectangle(
            BorderColor, x + 10, y + 10, width - 20, 92
        );

        DrawText("EXPRESSION", MutedTextColor, x + 20, y + 18);

        string expression = calcInput.Length > 0 ? calcInput : "0";

        DrawTextClipped(
            expression,
            TextColor,
            x + 20, y + 38,
            width - 40
        );

        int resultWidth = MeasureString(calcResult);

        DrawText(
            calcResult,
            Accent,
            x + width - resultWidth - 20,
            y + 70
        );

        int gridX = x + 10;
        int gridY = y + 116;
        int gridWidth = width - 20;
        int gridHeight = height - 126;

        if (gridHeight < 100)
            gridHeight = 100;

        int gap = 6;
        int buttonWidth = (gridWidth - 3 * gap) / 4;
        int buttonHeight = (gridHeight - 4 * gap) / 5;

        if (buttonHeight > 60)
            buttonHeight = 60;

        if (buttonHeight < 30)
            buttonHeight = 30;

        for (int i = 0; i < calcLabels.Length; i++)
        {
            int column = i % 4;
            int row = i / 4;

            int buttonX = gridX + column * (buttonWidth + gap);
            int buttonY = gridY + row * (buttonHeight + gap);

            DrawCalculatorButton(
                buttonX, buttonY, buttonWidth, buttonHeight,
                calcLabels[i]
            );
        }
    }

    private void DrawCalculatorButton(
        int x, int y, int width, int height, string label)
    {
        if (desktop == null)
            return;

        Color color;

        if (label == "=")
            color = Color.FromArgb(55, 165, 90);
        else if (label == "C")
            color = Color.FromArgb(190, 60, 75);
        else if (label == "DEL")
            color = Color.FromArgb(200, 130, 45);
        else if (label == "+" || label == "-" ||
                 label == "*" || label == "/")
            color = Accent;
        else
            color = PanelAltColor;

        bool hover = PointInRect(
            MouseManager.X, MouseManager.Y,
            x, y, width, height
        );

        desktop.DrawFilledRectangle(
            hover ? Blend(color, Color.White, 0.18f) : color,
            x, y, width, height
        );

        desktop.DrawRectangle(
            Blend(color, Color.Black, 0.35f),
            x, y, width, height
        );

        int textWidth = MeasureString(label);

        DrawText(
            label,
            Color.White,
            x + (width - textWidth) / 2,
            y + (height - 16) / 2
        );
    }

    private void PressCalculatorButton(string label)
    {
        if (label == "C")
        {
            calcInput = "";
            calcResult = "0";
            return;
        }

        if (label == "DEL")
        {
            if (calcInput.Length > 0)
            {
                calcInput = calcInput.Substring(
                    0, calcInput.Length - 1
                );
            }

            if (calcInput.Length == 0)
                calcResult = "0";

            return;
        }

        if (label == "=")
        {
            try
            {
                double value = EvaluateExpression(calcInput);
                calcResult = FormatNumber(value);
            }
            catch
            {
                calcResult = "Error";
            }

            return;
        }

        if (calcInput.Length < 48)
            calcInput += label;
    }

    private void HandleCalculatorClick(
        Window window, int mx, int my)
    {
        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        int gridX = x + 10;
        int gridY = y + 116;
        int gridWidth = width - 20;
        int gridHeight = height - 126;

        if (gridHeight < 100)
            gridHeight = 100;

        int gap = 6;
        int buttonWidth = (gridWidth - 3 * gap) / 4;
        int buttonHeight = (gridHeight - 4 * gap) / 5;

        if (buttonHeight > 60)
            buttonHeight = 60;

        if (buttonHeight < 30)
            buttonHeight = 30;

        for (int i = 0; i < calcLabels.Length; i++)
        {
            int column = i % 4;
            int row = i / 4;

            int buttonX = gridX + column * (buttonWidth + gap);
            int buttonY = gridY + row * (buttonHeight + gap);

            if (PointInRect(
                mx, my, buttonX, buttonY, buttonWidth, buttonHeight))
            {
                PressCalculatorButton(calcLabels[i]);
                return;
            }
        }
    }

    private void HandleCalculatorKey(KeyEvent key)
    {
        if (key.Key == Key.Enter || key.Key == Key.NumEnter)
        {
            PressCalculatorButton("=");
            return;
        }

        if (key.Key == Key.Backspace)
        {
            PressCalculatorButton("DEL");
            return;
        }

        if (key.Key == Key.Escape)
        {
            PressCalculatorButton("C");
            return;
        }

        char character = key.KeyChar;

        if ((character >= '0' && character <= '9') ||
            character == '.' || character == '+' ||
            character == '-' || character == '*' ||
            character == '/' || character == '(' ||
            character == ')')
        {
            PressCalculatorButton(character.ToString());
        }
    }

    private string calcParseText = "";
    private int calcParsePosition = 0;

    private double EvaluateExpression(string text)
    {
        if (text == null)
            text = "";

        if (text.Trim().Length == 0)
            return 0;

        calcParseText = text;
        calcParsePosition = 0;

        double value = ParseCalcExpression();
        SkipCalcSpaces();

        if (calcParsePosition < calcParseText.Length)
            throw new Exception("Unexpected character");

        return value;
    }

    private double ParseCalcExpression()
    {
        double value = ParseCalcTerm();

        while (true)
        {
            SkipCalcSpaces();

            if (calcParsePosition >= calcParseText.Length)
                break;

            char operation = calcParseText[calcParsePosition];

            if (operation != '+' && operation != '-')
                break;

            calcParsePosition++;

            double right = ParseCalcTerm();

            if (operation == '+')
                value += right;
            else
                value -= right;
        }

        return value;
    }

    private double ParseCalcTerm()
    {
        double value = ParseCalcFactor();

        while (true)
        {
            SkipCalcSpaces();

            if (calcParsePosition >= calcParseText.Length)
                break;

            char operation = calcParseText[calcParsePosition];

            if (operation != '*' && operation != '/')
                break;

            calcParsePosition++;

            double right = ParseCalcFactor();

            if (operation == '*')
                value *= right;
            else
                value /= right;
        }

        return value;
    }

    private double ParseCalcFactor()
    {
        SkipCalcSpaces();

        if (calcParsePosition >= calcParseText.Length)
            throw new Exception("Missing value");

        char current = calcParseText[calcParsePosition];

        if (current == '+')
        {
            calcParsePosition++;
            return ParseCalcFactor();
        }

        if (current == '-')
        {
            calcParsePosition++;
            return -ParseCalcFactor();
        }

        if (current == '(')
        {
            calcParsePosition++;

            double value = ParseCalcExpression();

            SkipCalcSpaces();

            if (calcParsePosition >= calcParseText.Length ||
                calcParseText[calcParsePosition] != ')')
            {
                throw new Exception("Missing parenthesis");
            }

            calcParsePosition++;
            return value;
        }

        int start = calcParsePosition;
        bool seenDot = false;

        while (calcParsePosition < calcParseText.Length)
        {
            char character = calcParseText[calcParsePosition];

            if (character >= '0' && character <= '9')
            {
                calcParsePosition++;
                continue;
            }

            if (character == '.' && !seenDot)
            {
                seenDot = true;
                calcParsePosition++;
                continue;
            }

            break;
        }

        if (calcParsePosition == start)
            throw new Exception("Invalid number");

        string number = calcParseText.Substring(
            start, calcParsePosition - start
        );

        double result;

        if (!double.TryParse(number, out result))
            throw new Exception("Invalid number");

        return result;
    }

    private void SkipCalcSpaces()
    {
        while (calcParsePosition < calcParseText.Length &&
               calcParseText[calcParsePosition] == ' ')
        {
            calcParsePosition++;
        }
    }

    private string FormatNumber(double value)
    {
        string text = value.ToString("F4");

        while (text.IndexOf('.') >= 0 && text.EndsWith("0"))
            text = text.Substring(0, text.Length - 1);

        if (text.EndsWith("."))
            text = text.Substring(0, text.Length - 1);

        if (text.Length == 0 || text == "-")
            text = "0";

        return text;
    }

    // ==========================================
    // CLOCK APP
    // ==========================================

    private void DrawClock(Window window)
    {
        if (desktop == null)
            return;

        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        desktop.DrawFilledRectangle(WindowColor, x, y, width, height);

        DateTime now = DateTime.Now;

        string time = now.ToString("HH:mm:ss");
        string date = now.ToString("dddd, dd MMMM yyyy");

        int timeWidth = MeasureString(time);
        int dateWidth = MeasureString(date);

        DrawText(
            time,
            TextColor,
            x + (width - timeWidth) / 2,
            y + 12
        );

        DrawText(
            date,
            MutedTextColor,
            x + (width - dateWidth) / 2,
            y + 32
        );

        int centerX = x + width / 2;
        int centerY = y + 145;
        int radius = Math.Min(width / 2 - 30, 82);

        if (radius < 40)
            radius = 40;

        desktop.DrawFilledCircle(
            PanelColor, centerX, centerY, radius
        );

        desktop.DrawCircle(
            Accent, centerX, centerY, radius
        );

        desktop.DrawCircle(
            BorderColor, centerX, centerY, radius - 2
        );

        for (int i = 0; i < 12; i++)
        {
            double angle = Math.PI * i * 30.0 / 180.0;
            int outerX = centerX + (int)(Math.Sin(angle) * (radius - 4));
            int outerY = centerY - (int)(Math.Cos(angle) * (radius - 4));
            int innerX = centerX + (int)(Math.Sin(angle) * (radius - 12));
            int innerY = centerY - (int)(Math.Cos(angle) * (radius - 12));

            desktop.DrawLine(
                BorderColor,
                innerX, innerY,
                outerX, outerY
            );
        }

        double hourAngle =
            (now.Hour % 12 + now.Minute / 60.0) * 30.0;

        double minuteAngle =
            (now.Minute + now.Second / 60.0) * 6.0;

        double secondAngle = now.Second * 6.0;

        DrawClockHand(
            centerX, centerY, hourAngle,
            radius / 2, 3, TextColor
        );

        DrawClockHand(
            centerX, centerY, minuteAngle,
            radius - 18, 2, TextColor
        );

        DrawClockHand(
            centerX, centerY, secondAngle,
            radius - 10, 1, Accent
        );

        desktop.DrawFilledCircle(
            Accent, centerX, centerY, 4
        );

        TimeSpan stopwatch = GetStopwatchElapsed();

        string stopwatchText =
            "Stopwatch: " + FormatStopwatch(stopwatch);

        int stopwatchWidth = MeasureString(stopwatchText);

        DrawText(
            stopwatchText,
            TextColor,
            x + (width - stopwatchWidth) / 2,
            y + 250
        );

        int buttonY = y + height - 72;
        int buttonWidth = 112;
        int startX = x + width / 2 - buttonWidth - 8;
        int resetX = x + width / 2 + 8;

        DrawSettingsButton(
            startX, buttonY, buttonWidth, 30,
            stopwatchRunning ? "Stop" : "Start"
        );

        DrawSettingsButton(
            resetX, buttonY, buttonWidth, 30, "Reset"
        );

        string uptime =
            "System uptime: " + FormatDuration(now - bootTime);

        DrawText(
            uptime,
            MutedTextColor,
            x + 14,
            y + height - 24
        );
    }

    private void DrawClockHand(
        int centerX, int centerY, double degrees,
        int length, int thickness, Color color)
    {
        if (desktop == null)
            return;

        double angle = Math.PI * degrees / 180.0;

        int endX = centerX + (int)(Math.Sin(angle) * length);
        int endY = centerY - (int)(Math.Cos(angle) * length);

        desktop.DrawLine(color, centerX, centerY, endX, endY);

        if (thickness > 1)
        {
            desktop.DrawLine(
                color, centerX + 1, centerY, endX + 1, endY
            );
        }
    }

    private TimeSpan GetStopwatchElapsed()
    {
        long ticks = stopwatchAccumulatedTicks;

        if (stopwatchRunning)
            ticks += DateTime.Now.Ticks - stopwatchStartTicks;

        if (ticks < 0)
            ticks = 0;

        return new TimeSpan(ticks);
    }

    private string FormatStopwatch(TimeSpan time)
    {
        int minutes = time.Minutes;
        int seconds = time.Seconds;
        int hundredths = time.Milliseconds / 10;

        return minutes.ToString("00") + ":" +
            seconds.ToString("00") + "." +
            hundredths.ToString("00");
    }

    private void ToggleStopwatch()
    {
        if (stopwatchRunning)
        {
            stopwatchAccumulatedTicks +=
                DateTime.Now.Ticks - stopwatchStartTicks;

            stopwatchRunning = false;
        }
        else
        {
            stopwatchStartTicks = DateTime.Now.Ticks;
            stopwatchRunning = true;
        }
    }

    private void ResetStopwatch()
    {
        stopwatchRunning = false;
        stopwatchStartTicks = 0;
        stopwatchAccumulatedTicks = 0;
    }

    private void HandleClockClick(
        Window window, int mx, int my)
    {
        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        int buttonY = y + height - 72;
        int buttonWidth = 112;
        int startX = x + width / 2 - buttonWidth - 8;
        int resetX = x + width / 2 + 8;

        if (PointInRect(
            mx, my, startX, buttonY, buttonWidth, 30))
        {
            ToggleStopwatch();
            return;
        }

        if (PointInRect(
            mx, my, resetX, buttonY, buttonWidth, 30))
        {
            ResetStopwatch();
        }
    }

    private void HandleClockKey(KeyEvent key)
    {
        if (key.Key == Key.Spacebar)
            ToggleStopwatch();
        else if (key.Key == Key.R)
            ResetStopwatch();
    }

    // ==========================================
    // ABOUT APP
    // ==========================================

    private void DrawAbout(Window window)
    {
        if (desktop == null)
            return;

        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        desktop.DrawFilledRectangle(WindowColor, x, y, width, height);

        DrawIconTile(
            x + 28, y + 26, 64,
            Accent, "C", false
        );

        DrawText("customOS", Accent, x + 110, y + 30);
        DrawText("A tiny hobby operating system", TextColor, x + 110, y + 50);
        DrawText("Version " + OS_VERSION, MutedTextColor, x + 110, y + 68);

        int lineY = y + 116;

        DrawText("Kernel: Cosmos Gen3", TextColor, x + 28, lineY);
        DrawText("Runtime: .NET 10", TextColor, x + 28, lineY + 22);
        DrawText("Architecture: " + RuntimeInformation.ProcessArchitecture,
            TextColor, x + 28, lineY + 44);
        DrawText("Creator: wkg2673", TextColor, x + 28, lineY + 66);
        DrawText("Language: C#", TextColor, x + 28, lineY + 88);

        int rightX = x + width / 2 + 10;

        DrawText("CPU: " + GetCpuName(), TextColor, rightX, lineY);
        DrawText("Cores: " + Environment.ProcessorCount,
            TextColor, rightX, lineY + 22);
        DrawText("Resolution: " + screenWidth + "x" + screenHeight,
            TextColor, rightX, lineY + 44);

        try
        {
            DrawText("Memory: " +
                FormatBytes(MemoryDiagnostics.RamSizeBytes),
                TextColor, rightX, lineY + 66);

            DrawText("Storage: " + StorageManager.DeviceCount +
                " device(s)", TextColor, rightX, lineY + 88);
        }
        catch
        {
            DrawText("Memory: unavailable", MutedTextColor,
                rightX, lineY + 66);
        }

        int featuresY = lineY + 128;

        DrawText("FEATURES", Accent, x + 28, featuresY);
        DrawText("- Multi-window desktop with drag, resize, minimize, maximize",
            TextColor, x + 28, featuresY + 24);
        DrawText("- Terminal, File Manager, System Monitor, Settings",
            TextColor, x + 28, featuresY + 44);
        DrawText("- Notepad, Calculator, Clock and About",
            TextColor, x + 28, featuresY + 64);
        DrawText("- Storage, network, memory and driver diagnostics",
            TextColor, x + 28, featuresY + 84);

        string footer =
            "Built with C# and Cosmos Gen3.  Uptime: " +
            FormatDuration(DateTime.Now - bootTime);

        DrawTextClipped(
            footer,
            MutedTextColor,
            x + 28,
            y + height - 24,
            width - 56
        );
    }

    // ==========================================
    // MOUSE INPUT
    // ==========================================

    private void HandleMouse()
    {
        if (desktop == null)
            return;

        int mx = MouseManager.X;
        int my = MouseManager.Y;

        bool left = MouseManager.LeftButton;
        bool right = MouseManager.RightButton;
        bool middle = MouseManager.MiddleButton;

        bool leftClick = left && !previousLeft;
        bool leftRelease = !left && previousLeft;
        bool rightClick = right && !previousRight;
        bool middleClick = middle && !previousMiddle;

        if (leftRelease)
        {
            dragWindow = null;
            resizeWindow = null;
        }

        if (left && dragWindow != null)
        {
            dragWindow.X = ClampValue(
                mx - dragOffsetX,
                0,
                screenWidth - 80
            );

            dragWindow.Y = ClampValue(
                my - dragOffsetY,
                0,
                screenHeight - TASKBAR_HEIGHT - 40
            );
        }

        if (left && resizeWindow != null)
        {
            int maxWidth = screenWidth - resizeWindow.X;

            if (maxWidth < 320)
                maxWidth = 320;

            int maxHeight = screenHeight - TASKBAR_HEIGHT -
                resizeWindow.Y;

            if (maxHeight < 240)
                maxHeight = 240;

            resizeWindow.Width = ClampValue(
                mx - resizeWindow.X, 320, maxWidth
            );

            resizeWindow.Height = ClampValue(
                my - resizeWindow.Y, 240, maxHeight
            );
        }

        int scroll = MouseManager.ScrollDelta;

        if (scroll != 0)
        {
            HandleScroll(scroll);
            MouseManager.ResetScrollDelta();
        }

        if (leftClick)
            HandleLeftClick(mx, my);

        if (rightClick)
            HandleRightClick(mx, my);

        if (middleClick)
        {
            Window? active = ActiveWindow;

            if (active != null)
                MinimizeWindow(active);
        }

        previousLeft = left;
        previousRight = right;
        previousMiddle = middle;
    }

    private void HandleScroll(int delta)
    {
        Window? window = WindowAt(
            MouseManager.X, MouseManager.Y
        );

        if (window == null)
            return;

        if (window.App == APP_TERMINAL)
        {
            terminalScroll += delta * 3;

            if (terminalScroll < 0)
                terminalScroll = 0;
        }
        else if (window.App == APP_FILES)
        {
            fileScroll -= delta;

            int maxScroll = GetFileMaxScroll(window);

            if (fileScroll > maxScroll)
                fileScroll = maxScroll;

            if (fileScroll < 0)
                fileScroll = 0;
        }
        else if (window.App == APP_NOTEPAD)
        {
            notepadScrollLine -= delta;

            int maxScroll = GetNotepadMaxScroll(window);

            if (notepadScrollLine > maxScroll)
                notepadScrollLine = maxScroll;

            if (notepadScrollLine < 0)
                notepadScrollLine = 0;
        }
    }

    private void HandleLeftClick(int mx, int my)
    {
        if (contextMenuOpen)
        {
            int item = GetContextMenuItemAt(mx, my);

            if (item >= 0)
            {
                int action = contextActions[item];
                contextMenuOpen = false;
                ExecuteContextAction(action);
                return;
            }

            contextMenuOpen = false;
        }

        if (startMenuOpen)
        {
            for (int i = 0; i < StartMenuItemCount; i++)
            {
                int itemX;
                int itemY;
                int itemW;
                int itemH;

                GetStartMenuItemRect(
                    i, out itemX, out itemY, out itemW, out itemH
                );

                if (PointInRect(mx, my, itemX, itemY, itemW, itemH))
                {
                    ActivateStartMenuItem(i);
                    return;
                }
            }

            startMenuOpen = false;
        }

        int taskbarApp;
        bool startButton;
        bool clock;

        if (HitTestTaskbar(
            mx, my, out taskbarApp, out startButton, out clock))
        {
            if (startButton)
            {
                startMenuOpen = !startMenuOpen;
                startMenuSelection = 0;
                contextMenuOpen = false;
                return;
            }

            if (taskbarApp != APP_NONE)
            {
                TaskbarActivate(taskbarApp);
                return;
            }

            if (clock)
            {
                OpenApplication(APP_CLOCK);
                return;
            }

            return;
        }

        Window? window = WindowAt(mx, my);

        if (window != null)
        {
            FocusWindow(window);

            int button = HitTestWindowButton(window, mx, my);

            if (button == 1)
            {
                CloseWindow(window);
                return;
            }

            if (button == 2)
            {
                ToggleMaximize(window);
                return;
            }

            if (button == 3)
            {
                MinimizeWindow(window);
                return;
            }

            if (my <= window.Y + TITLEBAR_HEIGHT)
            {
                long now = DateTime.Now.Ticks;

                if (window.App == lastTitleClickApp &&
                    now - lastTitleClickTicks < 5000000)
                {
                    ToggleMaximize(window);
                    lastTitleClickTicks = 0;
                    lastTitleClickApp = APP_NONE;
                    return;
                }

                lastTitleClickTicks = now;
                lastTitleClickApp = window.App;

                if (!window.Maximized)
                {
                    dragWindow = window;
                    dragOffsetX = mx - window.X;
                    dragOffsetY = my - window.Y;
                }

                return;
            }

            if (!window.Maximized &&
                mx >= window.X + window.Width - 16 &&
                my >= window.Y + window.Height - 16)
            {
                resizeWindow = window;
                return;
            }

            HandleAppClick(window, mx, my);
            return;
        }

        for (int i = 0; i < desktopApps.Length; i++)
        {
            int iconX;
            int iconY;

            GetDesktopIconRect(i, out iconX, out iconY);

            if (PointInRect(mx, my, iconX, iconY, 92, 88))
            {
                OpenApplication(desktopApps[i]);
                return;
            }
        }

        startMenuOpen = false;
        contextMenuOpen = false;
    }

    private void HandleRightClick(int mx, int my)
    {
        if (startMenuOpen)
            startMenuOpen = false;

        int taskbarApp;
        bool startButton;
        bool clock;

        if (HitTestTaskbar(
            mx, my, out taskbarApp, out startButton, out clock))
        {
            OpenContextMenu(MENU_TASKBAR, 0, mx, my);
            return;
        }

        Window? window = WindowAt(mx, my);

        if (window != null)
        {
            FocusWindow(window);

            if (window.App == APP_FILES)
            {
                int index = GetFileIndexAt(window, mx, my);

                if (index >= 0)
                {
                    fileSelected = index;
                    OpenContextMenu(MENU_FILE, index, mx, my);
                    return;
                }
            }

            OpenContextMenu(MENU_WINDOW, window.App, mx, my);
            return;
        }

        for (int i = 0; i < desktopApps.Length; i++)
        {
            int iconX;
            int iconY;

            GetDesktopIconRect(i, out iconX, out iconY);

            if (PointInRect(mx, my, iconX, iconY, 92, 88))
            {
                OpenContextMenu(
                    MENU_ICON, desktopApps[i], mx, my
                );

                return;
            }
        }

        OpenContextMenu(MENU_DESKTOP, 0, mx, my);
    }

    private void TaskbarActivate(int app)
    {
        Window? window = FindWindow(app);

        if (window != null)
        {
            if (window == ActiveWindow && !window.Minimized)
            {
                MinimizeWindow(window);
            }
            else
            {
                window.Minimized = false;
                FocusWindow(window);
            }
        }
        else
        {
            OpenApplication(app);
        }

        startMenuOpen = false;
        contextMenuOpen = false;
    }

    private Window? WindowAt(int mx, int my)
    {
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            Window window = windows[i];

            if (window.Minimized)
                continue;

            if (PointInRect(
                mx, my,
                window.X, window.Y,
                window.Width, window.Height))
            {
                return window;
            }
        }

        return null;
    }

    private void HandleAppClick(
        Window window, int mx, int my)
    {
        switch (window.App)
        {
            case APP_FILES:
                HandleFileManagerClick(window, mx, my);
                break;

            case APP_SYSTEM:
                HandleSystemMonitorClick(window, mx, my);
                break;

            case APP_SETTINGS:
                HandleSettingsClick(window, mx, my);
                break;

            case APP_NOTEPAD:
                HandleNotepadClick(window, mx, my);
                break;

            case APP_CALC:
                HandleCalculatorClick(window, mx, my);
                break;

            case APP_CLOCK:
                HandleClockClick(window, mx, my);
                break;
        }
    }

    private int GetFileIndexAt(
        Window window, int mx, int my)
    {
        int x = window.X + 1;
        int y = window.Y + TITLEBAR_HEIGHT;
        int width = window.Width - 2;
        int height = window.Height - TITLEBAR_HEIGHT - 1;

        int listY = y + 82;
        int statusY = y + height - 22;
        int rowHeight = 22;
        int maxRows = (statusY - listY - 4) / rowHeight;

        if (maxRows < 1)
            maxRows = 1;

        for (int i = fileScroll;
             i < fileEntries.Count && i < fileScroll + maxRows;
             i++)
        {
            int rowY = listY + (i - fileScroll) * rowHeight;

            if (PointInRect(
                mx, my, x + 8, rowY, width - 16, rowHeight))
            {
                return i;
            }
        }

        return -1;
    }

    private int GetFileMaxScroll(Window window)
    {
        int height = window.Height - TITLEBAR_HEIGHT - 1;
        int maxRows = (height - 82 - 26) / 22;

        if (maxRows < 1)
            maxRows = 1;

        int maxScroll = fileEntries.Count - maxRows;

        if (maxScroll < 0)
            maxScroll = 0;

        return maxScroll;
    }

    private int GetNotepadMaxScroll(Window window)
    {
        int height = window.Height - TITLEBAR_HEIGHT - 1;
        int maxLines = (height - 66 - 12) / 16;

        if (maxLines < 1)
            maxLines = 1;

        int maxScroll = CountNotepadLines() - maxLines;

        if (maxScroll < 0)
            maxScroll = 0;

        return maxScroll;
    }

    // ==========================================
    // KEYBOARD INPUT
    // ==========================================

    private void HandleKeyboard()
    {
        while (KeyboardManager.TryReadKey(out KeyEvent? key))
        {
            if (key == null)
                continue;

            // 1 == KeyEventType.Break (type is not visible here).
            if ((int)key.Type == 1)
                continue;

            bool control =
                (key.Modifiers & ConsoleModifiers.Control) != 0;

            bool alt =
                (key.Modifiers & ConsoleModifiers.Alt) != 0;

            bool shift =
                (key.Modifiers & ConsoleModifiers.Shift) != 0;

            if (key.Key == Key.LWin || key.Key == Key.RWin)
            {
                startMenuOpen = !startMenuOpen;
                startMenuSelection = 0;
                contextMenuOpen = false;
                continue;
            }

            if (alt && key.Key == Key.Tab)
            {
                CycleWindows(shift ? -1 : 1);
                continue;
            }

            if (alt && key.Key == Key.F4)
            {
                Window? active = ActiveWindow;

                if (active != null)
                    CloseWindow(active);

                continue;
            }

            if (key.Key == Key.F11)
            {
                Window? active = ActiveWindow;

                if (active != null)
                    ToggleMaximize(active);

                continue;
            }

            if (key.Key == Key.F5)
            {
                RefreshActiveApp();
                continue;
            }

            if (control && alt)
            {
                if (key.Key == Key.T)
                {
                    OpenApplication(APP_TERMINAL);
                    continue;
                }

                if (key.Key == Key.D)
                {
                    OpenApplication(APP_FILES);
                    continue;
                }

                if (key.Key == Key.S)
                {
                    OpenApplication(APP_SETTINGS);
                    continue;
                }

                if (key.Key == Key.M)
                {
                    OpenApplication(APP_SYSTEM);
                    continue;
                }

                if (key.Key == Key.N)
                {
                    OpenApplication(APP_NOTEPAD);
                    continue;
                }

                if (key.Key == Key.C)
                {
                    OpenApplication(APP_CALC);
                    continue;
                }

                if (key.Key == Key.K)
                {
                    OpenApplication(APP_CLOCK);
                    continue;
                }
            }

            if (key.Key == Key.Escape)
            {
                if (contextMenuOpen)
                {
                    contextMenuOpen = false;
                    continue;
                }

                if (startMenuOpen)
                {
                    startMenuOpen = false;
                    continue;
                }

                Window? active = ActiveWindow;

                if (active != null)
                {
                    CloseWindow(active);
                    continue;
                }
            }

            if (startMenuOpen)
            {
                if (key.Key == Key.UpArrow)
                {
                    startMenuSelection--;

                    if (startMenuSelection < 0)
                        startMenuSelection = StartMenuItemCount - 1;

                    continue;
                }

                if (key.Key == Key.DownArrow)
                {
                    startMenuSelection++;

                    if (startMenuSelection >= StartMenuItemCount)
                        startMenuSelection = 0;

                    continue;
                }

                if (key.Key == Key.Enter)
                {
                    ActivateStartMenuItem(startMenuSelection);
                    continue;
                }
            }

            Window? current = ActiveWindow;

            if (current == null)
            {
                if (key.Key == Key.D1)
                    OpenApplication(APP_TERMINAL);
                else if (key.Key == Key.D2)
                    OpenApplication(APP_FILES);
                else if (key.Key == Key.D3)
                    OpenApplication(APP_SYSTEM);
                else if (key.Key == Key.D4)
                    OpenApplication(APP_SETTINGS);
                else if (key.Key == Key.D5)
                    OpenApplication(APP_NOTEPAD);
                else if (key.Key == Key.D6)
                    OpenApplication(APP_CALC);
                else if (key.Key == Key.D7)
                    OpenApplication(APP_CLOCK);
                else if (key.Key == Key.D8)
                    OpenApplication(APP_ABOUT);

                continue;
            }

            switch (current.App)
            {
                case APP_TERMINAL:
                    HandleTerminalKey(key, control, alt, shift);
                    break;

                case APP_FILES:
                    HandleFilesKey(key);
                    break;

                case APP_SYSTEM:
                    HandleSystemKey(key);
                    break;

                case APP_SETTINGS:
                    HandleSettingsKey(key);
                    break;

                case APP_NOTEPAD:
                    HandleNotepadKey(key, control, shift);
                    break;

                case APP_CALC:
                    HandleCalculatorKey(key);
                    break;

                case APP_CLOCK:
                    HandleClockKey(key);
                    break;
            }
        }
    }

    private int ClampValue(int value, int minimum, int maximum)
    {
        if (maximum < minimum)
            maximum = minimum;

        if (value < minimum)
            return minimum;

        if (value > maximum)
            return maximum;

        return value;
    }

    // ==========================================
    // DRAWING HELPERS
    // ==========================================

    private bool PointInRect(
        int x, int y, int left, int top, int width, int height)
    {
        return x >= left && x < left + width &&
               y >= top && y < top + height;
    }

    private int MeasureString(string text)
    {
        if (text == null)
            return 0;

        return GetUiFont().MeasureString(text);
    }

    private PCScreenFont GetUiFont()
    {
        if (uiFont == null)
            return PCScreenFont.DefaultFont;

        return uiFont;
    }

    private void DrawText(string text, Color color, int x, int y)
    {
        if (desktop == null || text == null)
            return;

        desktop.DrawString(
            text,
            GetUiFont(),
            color,
            x,
            y
        );
    }

    private void DrawTextClipped(
        string text, Color color, int x, int y, int maxWidth)
    {
        if (text == null || maxWidth <= 0)
            return;

        int characterWidth = GetUiFont().Width;
        int maximumCharacters = maxWidth / characterWidth;

        if (maximumCharacters < 1)
            return;

        if (text.Length > maximumCharacters)
        {
            if (maximumCharacters <= 3)
            {
                text = text.Substring(0, maximumCharacters);
            }
            else
            {
                text = text.Substring(0, maximumCharacters - 3) + "...";
            }
        }

        DrawText(text, color, x, y);
    }

    private void DrawMouseCursor()
    {
        if (desktop == null)
            return;

        int baseX = MouseManager.X;
        int baseY = MouseManager.Y;

        if (baseX > screenWidth - 20)
            baseX = screenWidth - 20;

        if (baseY > screenHeight - 20)
            baseY = screenHeight - 20;

        if (baseX < 0)
            baseX = 0;

        if (baseY < 0)
            baseY = 0;

        int[] pointX = { 0, 0, 4, 6, 8, 6, 11, 0 };
        int[] pointY = { 0, 15, 11, 16, 15, 10, 10, 0 };

        int pointCount = pointX.Length;
        int minY = pointY[0];
        int maxY = pointY[0];

        for (int i = 1; i < pointCount; i++)
        {
            minY = Math.Min(minY, pointY[i]);
            maxY = Math.Max(maxY, pointY[i]);
        }

        for (int scanY = minY; scanY <= maxY; scanY++)
        {
            List<float> intersections = new List<float>();

            for (int i = 0; i < pointCount - 1; i++)
            {
                int x1 = pointX[i];
                int y1 = pointY[i];
                int x2 = pointX[i + 1];
                int y2 = pointY[i + 1];

                if ((y1 <= scanY && y2 > scanY) ||
                    (y2 <= scanY && y1 > scanY))
                {
                    float intersectX = x1 +
                        (float)(scanY - y1) * (x2 - x1) /
                        (y2 - y1);

                    intersections.Add(intersectX);
                }
            }

            intersections.Sort();

            for (int i = 0; i + 1 < intersections.Count; i += 2)
            {
                int startX = (int)Math.Ceiling(intersections[i]);
                int endX = (int)Math.Floor(intersections[i + 1]);

                desktop.DrawLine(
                    Color.White,
                    baseX + startX,
                    baseY + scanY,
                    baseX + endX,
                    baseY + scanY
                );
            }
        }

        for (int i = 0; i < pointCount - 1; i++)
        {
            desktop.DrawLine(
                Color.Black,
                baseX + pointX[i],
                baseY + pointY[i],
                baseX + pointX[i + 1],
                baseY + pointY[i + 1]
            );
        }
    }

    // ==========================================
    // FORMATTING HELPERS
    // ==========================================

    private string FormatBytes(ulong bytes)
    {
        double value = bytes;

        if (bytes >= 1073741824UL)
            return (value / 1073741824.0).ToString("F1") + " GB";

        if (bytes >= 1048576UL)
            return (value / 1048576.0).ToString("F1") + " MB";

        if (bytes >= 1024UL)
            return (value / 1024.0).ToString("F1") + " KB";

        return bytes + " B";
    }

    private string FormatDuration(TimeSpan time)
    {
        if (time.TotalDays >= 1)
        {
            return ((int)time.TotalDays) + "d " +
                time.Hours + "h " + time.Minutes + "m";
        }

        if (time.TotalHours >= 1)
        {
            return ((int)time.TotalHours) + "h " +
                time.Minutes + "m " + time.Seconds + "s";
        }

        if (time.TotalMinutes >= 1)
            return time.Minutes + "m " + time.Seconds + "s";

        return time.Seconds + "s";
    }

    private string MakeBar(int percent, int width)
    {
        if (percent < 0)
            percent = 0;

        if (percent > 100)
            percent = 100;

        if (width < 1)
            width = 1;

        int filled = percent * width / 100;
        StringBuilder builder = new StringBuilder();

        builder.Append("[");

        for (int i = 0; i < width; i++)
            builder.Append(i < filled ? '#' : '-');

        builder.Append("]");

        return builder.ToString();
    }

    // ==========================================
    // PATH HELPERS
    // ==========================================

    private string ResolvePath(string baseDirectory, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return NormalizePath(baseDirectory);

        path = path.Trim();

        if (path == "~")
            return "/mnt";

        if (path.StartsWith("/"))
            return NormalizePath(path);

        if (path == "..")
            return GetParentPath(baseDirectory);

        if (path.StartsWith("../"))
        {
            string result = NormalizePath(baseDirectory);
            string rest = path;

            while (rest.StartsWith("../"))
            {
                result = GetParentPath(result);
                rest = rest.Substring(3);
            }

            if (rest.Length == 0)
                return result;

            return NormalizePath(JoinPath(result, rest));
        }

        return NormalizePath(JoinPath(baseDirectory, path));
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        path = path.Replace('\\', '/');

        if (!path.StartsWith("/"))
            path = "/" + path;

        string[] parts = path.Split('/');
        List<string> segments = new List<string>();

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];

            if (part.Length == 0 || part == ".")
                continue;

            if (part == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);

                continue;
            }

            segments.Add(part);
        }

        if (segments.Count == 0)
            return "/";

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < segments.Count; i++)
        {
            builder.Append("/");
            builder.Append(segments[i]);
        }

        return builder.ToString();
    }

    private string GetParentPath(string path)
    {
        path = NormalizePath(path);

        if (path == "/")
            return "/";

        int index = path.LastIndexOf('/');

        if (index <= 0)
            return "/";

        return path.Substring(0, index);
    }

    private string GetNameFromPath(string path)
    {
        if (path == null)
            return "";

        path = path.Replace('\\', '/');

        while (path.Length > 1 && path.EndsWith("/"))
            path = path.Substring(0, path.Length - 1);

        int index = path.LastIndexOf('/');

        if (index >= 0)
            return path.Substring(index + 1);

        return path;
    }

    private string JoinPath(string directory, string name)
    {
        if (string.IsNullOrWhiteSpace(directory))
            directory = "/";

        if (directory.EndsWith("/"))
            return NormalizePath(directory + name);

        return NormalizePath(directory + "/" + name);
    }

    private long GetFileSize(string path)
    {
        try
        {
            VfsStat stat = default(VfsStat);

            if (VfsManager.TryStat(path, out stat))
                return (long)stat.Size;
        }
        catch
        {
        }

        return 0;
    }

    // ==========================================
    // HARDWARE INFORMATION
    // ==========================================

    private string GetCpuName()
    {
        if (!X86Base.IsSupported)
            return "Unknown CPU";

        try
        {
            var maximum = X86Base.CpuId(
                unchecked((int)0x80000000), 0
            );

            if (unchecked((uint)maximum.Eax) < 0x80000004)
                return "Unknown CPU";

            StringBuilder builder = new StringBuilder();

            for (int leaf = unchecked((int)0x80000002);
                 leaf <= unchecked((int)0x80000004);
                 leaf++)
            {
                var result = X86Base.CpuId(leaf, 0);

                AppendRegister(builder, result.Eax);
                AppendRegister(builder, result.Ebx);
                AppendRegister(builder, result.Ecx);
                AppendRegister(builder, result.Edx);
            }

            return builder.ToString().Trim(
                '\0', ' ', '\t', '\r', '\n'
            );
        }
        catch
        {
            return "CPU detection failed";
        }
    }

    private void AppendRegister(StringBuilder builder, int register)
    {
        for (int i = 0; i < 4; i++)
            builder.Append((char)((register >> (i * 8)) & 0xFF));
    }

    private void ShowMemory()
    {
        try
        {
            ulong ram = MemoryDiagnostics.RamSizeBytes;
            ulong pageSize = MemoryDiagnostics.PageSizeBytes;
            ulong totalPages = MemoryDiagnostics.TotalPages;
            ulong freePages = MemoryDiagnostics.FreePages;
            ulong usedPages = totalPages - freePages;

            Console.WriteLine("Physical RAM: " + FormatBytes(ram));
            Console.WriteLine("Page size: " + pageSize + " bytes");
            Console.WriteLine("Pages: " + usedPages + " used / " +
                totalPages + " total");
            Console.WriteLine("Free pages: " + freePages);

            if (totalPages > 0)
            {
                int percent = (int)(usedPages * 100 / totalPages);
                Console.WriteLine("Usage: " + MakeBar(percent, 24) +
                    " " + percent + "%");
            }

            Console.WriteLine("Managed heap: " +
                FormatBytes((ulong)GC.GetTotalMemory(false)));
            Console.WriteLine("GC time: " +
                MemoryDiagnostics.GcTimePercent + "%");
            Console.WriteLine("Collections: " +
                MemoryDiagnostics.TotalCollections);
            Console.WriteLine("Objects freed: " +
                MemoryDiagnostics.TotalObjectsFreed);
        }
        catch
        {
            Console.WriteLine("Memory diagnostics unavailable.");
        }
    }

    private void ShowDisks()
    {
        try
        {
            if (!StorageManager.IsEnabled)
            {
                Console.WriteLine("Storage subsystem is disabled.");
                return;
            }

            Console.WriteLine("Storage devices: " +
                StorageManager.DeviceCount);

            for (int i = 0; i < StorageManager.DeviceCount; i++)
            {
                IBlockDevice device = StorageManager.GetDevice(i);

                Console.WriteLine(
                    "[" + i + "] " + device.Name + "  " +
                    FormatBytes(device.BlockCount * device.BlockSize) +
                    "  (" + device.BlockCount + " x " +
                    device.BlockSize + ")"
                );
            }

            IReadOnlyList<Partition> partitions = StorageManager.Partitions;

            Console.WriteLine("Partitions: " + partitions.Count);

            for (int i = 0; i < partitions.Count; i++)
            {
                Partition partition = partitions[i];

                Console.WriteLine(
                    "  " + partition.Name + "  " +
                    FormatBytes(partition.BlockCount * partition.BlockSize)
                );
            }
        }
        catch
        {
            Console.WriteLine("Storage information unavailable.");
        }
    }

    private void ShowNetwork()
    {
        try
        {
            if (!NetworkManager.IsEnabled)
            {
                Console.WriteLine("Network subsystem is disabled.");
                return;
            }

            Console.WriteLine("Network adapters: " +
                NetworkManager.DeviceCount);

            for (int i = 0; i < NetworkManager.DeviceCount; i++)
            {
                NetworkAdapter adapter = NetworkManager.GetAdapter(i);

                Console.WriteLine("[" + i + "] " + adapter.Name);
                Console.WriteLine("    MAC: " +
                    adapter.MacAddress.ToString());
                Console.WriteLine("    Link: " +
                    (adapter.LinkUp ? "up" : "down") +
                    "  Ready: " + adapter.Ready);

                IPConfig? config = adapter.IPConfig;

                if (config != null)
                {
                    Console.WriteLine("    IP: " +
                        config.Address.ToString());
                    Console.WriteLine("    Mask: " +
                        config.SubnetMask.ToString());
                    Console.WriteLine("    Gateway: " +
                        config.DefaultGateway.ToString());
                }
            }

            Console.WriteLine("Hostname: " + DnsConfig.HostName);

            IReadOnlyList<Address> dns = DnsConfig.Nameservers;

            for (int i = 0; i < dns.Count; i++)
                Console.WriteLine("DNS " + (i + 1) + ": " +
                    dns[i].ToString());
        }
        catch
        {
            Console.WriteLine("Network information unavailable.");
        }
    }

    private void ShowDrivers()
    {
        try
        {
            if (!DriverDiagnostics.IsStarted)
            {
                Console.WriteLine("Driver diagnostics not started.");
                return;
            }

            Console.WriteLine("Drivers: " + DriverDiagnostics.DriverCount);
            Console.WriteLine("Device nodes: " +
                DriverDiagnostics.NodeCount);
            Console.WriteLine("Published devices: " +
                DriverDiagnostics.DeviceCount);
            Console.WriteLine("Held resources: " +
                DriverDiagnostics.GetTotalHeldResourceCount());

            for (int i = 0; i < DriverDiagnostics.DriverCount; i++)
            {
                DriverInfo info = default(DriverInfo);

                if (DriverDiagnostics.TryGetDriver(i, out info))
                {
                    Console.WriteLine("  " + info.Name +
                        " (priority " + info.Priority + ")");
                }
            }
        }
        catch
        {
            Console.WriteLine("Driver diagnostics unavailable.");
        }
    }

    private void ShowMounts()
    {
        try
        {
            IReadOnlyList<VfsMount> mounts = VfsManager.Mounts;

            Console.WriteLine("Mounts: " + mounts.Count);

            for (int i = 0; i < mounts.Count; i++)
            {
                VfsMount mount = mounts[i];

                Console.WriteLine(
                    mount.MountPoint + " <- " + mount.Source +
                    (mount.IsReadOnly ? " (ro)" : " (rw)")
                );
            }
        }
        catch
        {
            Console.WriteLine("Mount information unavailable.");
        }
    }

    private void ShowDiskUsage()
    {
        try
        {
            IReadOnlyList<VfsMount> mounts = VfsManager.Mounts;

            Console.WriteLine("Filesystem usage:");

            for (int i = 0; i < mounts.Count; i++)
            {
                string point = mounts[i].MountPoint;
                VfsStatFs stat = default(VfsStatFs);

                if (VfsManager.TryStatFs(point, out stat) &&
                    stat.BlockSize > 0)
                {
                    ulong total = stat.BlockSize * stat.Blocks;
                    ulong free = stat.BlockSize * stat.FreeBlocks;
                    ulong used = total - free;
                    int percent = total > 0
                        ? (int)(used * 100 / total)
                        : 0;

                    Console.WriteLine(
                        point + "  " + FormatBytes(used) + " / " +
                        FormatBytes(total) + "  " + percent + "%"
                    );
                }
                else
                {
                    Console.WriteLine(point + "  (usage unavailable)");
                }
            }
        }
        catch
        {
            Console.WriteLine("Disk usage unavailable.");
        }
    }

    private void ShowNeofetch()
    {
        Console.WriteLine("      ______");
        Console.WriteLine("   .-'      '-.");
        Console.WriteLine("  /  customOS  \\");
        Console.WriteLine(" |   v" + OS_VERSION + "      |");
        Console.WriteLine("  \\            /");
        Console.WriteLine("   '-.______.-'");
        Console.WriteLine();

        try
        {
            Console.WriteLine("OS: customOS " + OS_VERSION + " x64");
            Console.WriteLine("Kernel: Cosmos Gen3");
            Console.WriteLine("CPU: " + GetCpuName());
            Console.WriteLine("Cores: " + Environment.ProcessorCount);
            Console.WriteLine("Memory: " +
                FormatBytes(MemoryDiagnostics.RamSizeBytes));
            Console.WriteLine("Storage: " +
                StorageManager.DeviceCount + " device(s)");
            Console.WriteLine("Network: " +
                NetworkManager.DeviceCount + " adapter(s)");
            Console.WriteLine("Uptime: " +
                FormatDuration(DateTime.Now - bootTime));
        }
        catch
        {
            Console.WriteLine("System details unavailable.");
        }
    }

    // ==========================================
    // CONSOLE FILESYSTEM COMMANDS
    // ==========================================

    private void ListDirectory(string argument)
    {
        string target = ResolvePath(terminalDirectory, argument);

        try
        {
            if (!Directory.Exists(target))
            {
                Console.WriteLine("Directory not found: " + target);
                return;
            }

            Console.WriteLine("Directory: " + target);

            string[] directories = Directory.GetDirectories(target);
            string[] files = Directory.GetFiles(target);

            for (int i = 0; i < directories.Length; i++)
            {
                Console.WriteLine(
                    "[DIR]  " + GetNameFromPath(directories[i])
                );
            }

            for (int i = 0; i < files.Length; i++)
            {
                Console.WriteLine(
                    "[FILE] " + GetNameFromPath(files[i]) +
                    "  " + FormatBytes((ulong)GetFileSize(files[i]))
                );
            }

            if (directories.Length == 0 && files.Length == 0)
                Console.WriteLine("(empty)");

            Console.WriteLine(
                directories.Length + " dir(s), " +
                files.Length + " file(s)"
            );
        }
        catch
        {
            Console.WriteLine("Cannot read directory.");
        }
    }

    private void ChangeDirectory(string argument)
    {
        string target;

        if (string.IsNullOrWhiteSpace(argument) || argument == "~")
            target = "/mnt";
        else if (argument == "..")
            target = GetParentPath(terminalDirectory);
        else
            target = ResolvePath(terminalDirectory, argument);

        try
        {
            if (Directory.Exists(target))
                terminalDirectory = NormalizePath(target);
            else
                Console.WriteLine("Directory not found: " + target);
        }
        catch
        {
            Console.WriteLine("Filesystem unavailable.");
        }
    }

    private void ReadFile(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: cat filename");
            return;
        }

        try
        {
            string target = ResolvePath(terminalDirectory, argument);

            if (!File.Exists(target))
            {
                Console.WriteLine("File not found: " + target);
                return;
            }

            string text = File.ReadAllText(target);
            string[] lines = text.Split('\n');

            for (int i = 0; i < lines.Length && i < 200; i++)
                Console.WriteLine(lines[i].Replace("\r", ""));

            if (lines.Length > 200)
                Console.WriteLine("... output truncated ...");
        }
        catch
        {
            Console.WriteLine("Cannot read file.");
        }
    }

    private void WriteFile(string argument)
    {
        int separator = argument.IndexOf(' ');

        if (separator <= 0)
        {
            Console.WriteLine("Usage: write filename text");
            return;
        }

        string path = ResolvePath(
            terminalDirectory,
            argument.Substring(0, separator)
        );

        string content = argument.Substring(separator + 1);

        try
        {
            File.WriteAllText(path, content);
            Console.WriteLine("Wrote " + content.Length + " chars to " + path);
        }
        catch
        {
            Console.WriteLine("Write failed.");
        }
    }

    private void CreateDirectory(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: mkdir name");
            return;
        }

        try
        {
            string target = ResolvePath(terminalDirectory, argument);

            Directory.CreateDirectory(target);
            Console.WriteLine("Created " + target);
        }
        catch
        {
            Console.WriteLine("Could not create directory.");
        }
    }

    private void DeleteFile(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: rm filename");
            return;
        }

        try
        {
            string target = ResolvePath(terminalDirectory, argument);

            if (File.Exists(target))
            {
                File.Delete(target);
                Console.WriteLine("Deleted " + target);
            }
            else
            {
                Console.WriteLine("File not found.");
            }
        }
        catch
        {
            Console.WriteLine("Delete failed.");
        }
    }

    private void RemoveDirectory(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: rmdir name");
            return;
        }

        try
        {
            string target = ResolvePath(terminalDirectory, argument);

            if (Directory.Exists(target))
            {
                Directory.Delete(target);
                Console.WriteLine("Removed " + target);
            }
            else
            {
                Console.WriteLine("Directory not found.");
            }
        }
        catch
        {
            Console.WriteLine("Directory must be empty or cannot be removed.");
        }
    }

    private void TouchFile(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: touch filename");
            return;
        }

        try
        {
            string target = ResolvePath(terminalDirectory, argument);

            if (!File.Exists(target))
                File.WriteAllText(target, "");

            Console.WriteLine("Touched " + target);
        }
        catch
        {
            Console.WriteLine("Could not create file.");
        }
    }

    private void CopyFile(string argument)
    {
        string[] parts = argument.Split(
            ' ', StringSplitOptions.RemoveEmptyEntries
        );

        if (parts.Length < 2)
        {
            Console.WriteLine("Usage: cp source destination");
            return;
        }

        try
        {
            string source = ResolvePath(terminalDirectory, parts[0]);
            string destination = ResolvePath(terminalDirectory, parts[1]);

            if (!File.Exists(source))
            {
                Console.WriteLine("Source file not found.");
                return;
            }

            File.WriteAllText(destination, File.ReadAllText(source));
            Console.WriteLine("Copied to " + destination);
        }
        catch
        {
            Console.WriteLine("Copy failed.");
        }
    }

    // ==========================================
    // CONSOLE INFORMATION COMMANDS
    // ==========================================

    private void Help()
    {
        Console.WriteLine("customOS " + OS_VERSION + " commands");
        Console.WriteLine();
        Console.WriteLine("System");
        Console.WriteLine("  help              Show this command list");
        Console.WriteLine("  about             About customOS");
        Console.WriteLine("  version           Print the OS version");
        Console.WriteLine("  sysinfo           Full hardware summary");
        Console.WriteLine("  memory | free     Physical memory report");
        Console.WriteLine("  disks             Storage devices and partitions");
        Console.WriteLine("  df                Filesystem usage");
        Console.WriteLine("  mounts            Mounted filesystems");
        Console.WriteLine("  network | ipconfig  Adapter and IP information");
        Console.WriteLine("  drivers           Driver diagnostics");
        Console.WriteLine("  neofetch          Compact system banner");
        Console.WriteLine("  uptime            Time since boot");
        Console.WriteLine("  time | date       Current clock and date");
        Console.WriteLine("  whoami            Current user");
        Console.WriteLine("  commands          Command counter");
        Console.WriteLine("  history           Command history");
        Console.WriteLine("  calc <expr>       Evaluate an expression");
        Console.WriteLine("  reboot            Restart the machine");
        Console.WriteLine("  shutdown | exit   Power off the machine");
        Console.WriteLine();
        Console.WriteLine("Files");
        Console.WriteLine("  ls [path]         List a directory");
        Console.WriteLine("  cd [path]         Change directory");
        Console.WriteLine("  pwd               Print working directory");
        Console.WriteLine("  cat <file>        Show file contents");
        Console.WriteLine("  write <file> <t>  Write text to a file");
        Console.WriteLine("  touch <file>      Create an empty file");
        Console.WriteLine("  mkdir <dir>       Create a directory");
        Console.WriteLine("  rmdir <dir>       Remove an empty directory");
        Console.WriteLine("  rm | del <file>   Delete a file");
        Console.WriteLine("  cp <src> <dst>    Copy a file");
        Console.WriteLine();
        Console.WriteLine("Desktop");
        Console.WriteLine("  gui               Start the graphical desktop");
        Console.WriteLine("  theme             Toggle dark/light theme");
        Console.WriteLine("  wallpaper         Cycle the wallpaper");
        Console.WriteLine("  settings          Show current settings");
        Console.WriteLine("  clear             Clear the screen");
        Console.WriteLine("  echo <text>       Print text");
    }

    private void About()
    {
        Console.WriteLine("customOS " + OS_VERSION);
        Console.WriteLine("A hobby operating system built with the Cosmos SDK.");
        Console.WriteLine();
        Console.WriteLine("Kernel:    Cosmos Gen3");
        Console.WriteLine("Target:    x64, multiboot");
        Console.WriteLine("Desktop:   custom multi-window shell");
        Console.WriteLine("Apps:      Terminal, Files, System Monitor,");
        Console.WriteLine("           Settings, Notepad, Calculator, Clock");
        Console.WriteLine();
        Console.WriteLine("Type 'gui' for the desktop or 'help' for commands.");
    }

    private void ShowHistory()
    {
        if (commandHistory.Count == 0)
        {
            Console.WriteLine("No commands in history.");
            return;
        }

        for (int i = 0; i < commandHistory.Count; i++)
            Console.WriteLine((i + 1) + "  " + commandHistory[i]);
    }

    private void Calculate(string argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            Console.WriteLine("Usage: calc <expression>");
            Console.WriteLine("Example: calc (2 + 3) * 4");
            return;
        }

        try
        {
            double result = EvaluateExpression(argument);
            Console.WriteLine(argument + " = " + FormatNumber(result));
        }
        catch
        {
            Console.WriteLine("Invalid expression.");
        }
    }

    private void ShowSystemInfo()
    {
        Console.WriteLine("=== customOS system information ===");
        Console.WriteLine("OS:      " + OS_NAME + " " + OS_VERSION);
        Console.WriteLine("Kernel:  Cosmos Gen3");
        Console.WriteLine("Uptime:  " + FormatDuration(DateTime.Now - bootTime));
        Console.WriteLine();

        try
        {
            Console.WriteLine("CPU:     " + GetCpuName());
            Console.WriteLine("Cores:   " + Environment.ProcessorCount);
            Console.WriteLine("Memory:  " +
                FormatBytes(MemoryDiagnostics.RamSizeBytes) + " (" +
                FormatBytes(MemoryDiagnostics.PageSizeBytes) + " pages)");
        }
        catch
        {
            Console.WriteLine("CPU/memory details unavailable.");
        }

        try
        {
            Console.WriteLine("Storage: " +
                StorageManager.DeviceCount + " device(s), " +
                StorageManager.Partitions.Count + " partition(s)");
        }
        catch
        {
            Console.WriteLine("Storage details unavailable.");
        }

        try
        {
            Console.WriteLine("Network: " +
                NetworkManager.DeviceCount + " adapter(s)");
        }
        catch
        {
            Console.WriteLine("Network details unavailable.");
        }

        try
        {
            if (DisplayManager.Primary != null)
            {
                Console.WriteLine(
                    "Display: " + DisplayManager.Primary.Width + "x" +
                    DisplayManager.Primary.Height + " @ " +
                    DisplayManager.Primary.BitsPerPixel + "bpp"
                );
            }
        }
        catch
        {
            Console.WriteLine("Display details unavailable.");
        }

        try
        {
            Console.WriteLine("Drivers: " +
                DriverDiagnostics.DriverCount + " driver(s)");
        }
        catch
        {
            Console.WriteLine("Driver details unavailable.");
        }
    }

    private void Reboot()
    {
        Console.WriteLine("Rebooting...");

        try
        {
            Sys.Power.Reboot();
        }
        catch
        {
            Console.WriteLine("Reboot is not supported on this platform.");
        }
    }

    private void Shutdown()
    {
        Console.WriteLine("Shutting down...");

        try
        {
            Sys.Power.Shutdown();
        }
        catch
        {
            Console.WriteLine("Shutdown is not supported on this platform.");
        }
    }

    // ==========================================
    // UI FONT
    // ==========================================
    // Compact 8x16 PSF1 bitmap font (Spleen, BSD-2-Clause) embedded so the
    // desktop uses the compact metrics its layout was designed around.

    private const string UI_FONT_DATA = "NgQCEAAAAAAAAAAAAAAAAAAAAAAAAH6BpYGBpZmBgX4AAAAAAAB+/9v//9vn//9+AAAAAAAAAABs/v7+fDgQAAAAAAAAAAAAEDh8/nw4EAAAAAAAAAAAGDw85+fnGBg8AAAAAAAAABg8fv//fhgYPAAAAAAAAAAAAAAYPDwYAAAAAAAA////////58PD5////////wAAAAAAPGZmZmY8AAAAAAD//////8OZmZmZw///////AAAPBw0ZPGZmZmY8AAAAAAAAPGZmZmY8GDwYGAAAAAAAAH5mfmBgYGBgwMAAAAAAAAB+Zn5mZmZmZm7MwAAAAAAAABgY2zznPNsYGAAAAACAwODw+Pz+///+/Pjw4MCAAQMHDx8/f///fz8fDwcDAQAAGDx+GBgYGH48GAAAAAAAAGZmZmZmZmYAZmYAAAAAAAB+1tbWdhYWFhYWAAAAAAA8ZmAwPGZmZmY8DAZmPAAAAAAAAAAAAP7+/v4AAAAAAAAYPH4YGBgYfjwYfgAAAAAAGDx+GBgYGBgYGAAAAAAAABgYGBgYGBh+PBgAAAAAAAAAAAAIBP4ECAAAAAAAAAAAAAAAIED+QCAAAAAAAAAAAAAAAADAwMD+AAAAAAAAAAAAAAAobP5sKAAAAAAAAAAAAAAAEDg4fHz+/gAAAAAAAAAAAP7+fHw4OBAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAGBgYGBgYGAAYGAAAAAAAZmZmZgAAAAAAAAAAAAAAAABsbP5sbGxs/mxsAAAAAAAQftDQ0HwWFhYW/BAAAAAAAAZmbAwYGDA2ZmAAAAAAAAA4bGxsOHDazMx6AAAAAAAYGBgYAAAAAAAAAAAAAAAADhgwMGBgYGAwMBgOAAAAAHAYDAwGBgYGDAwYcAAAAAAAAABmPBj/GDxmAAAAAAAAAAAAABgYfhgYAAAAAAAAAAAAAAAAAAAAABgYMAAAAAAAAAAAAAB+AAAAAAAAAAAAAAAAAAAAAAAAGBgAAAAAAAYGDAwYGDAwYGDAwAAAAAAAfMbGzt725sbGfAAAAAAAABg4eFgYGBgYGH4AAAAAAAB8xgYGDBgwYMb+AAAAAAAAfMYGBjwGBgbGfAAAAAAAAMDAzMzMzP4MDAwAAAAAAAD+xsDA/AYGBsZ8AAAAAAAAfMbAwPzGxsbGfAAAAAAAAP7GBgYMGDAwMDAAAAAAAAB8xsbGfMbGxsZ8AAAAAAAAfMbGxsZ+BgbGfAAAAAAAAAAAABgYAAAAGBgAAAAAAAAAAAAYGAAAABgYMAAAAAAABgwYMGBgMBgMBgAAAAAAAAAAAH4AAH4AAAAAAAAAAABgMBgMBgYMGDBgAAAAAAAAfMYGDBgwMAAwMAAAAAAAAAB8wtra2trewHwAAAAAAAB8xsbG/sbGxsbGAAAAAAAA/MbGxvzGxsbG/AAAAAAAAH7AwMDAwMDAwH4AAAAAAAD8xsbGxsbGxsb8AAAAAAAAfsDAwPjAwMDAfgAAAAAAAH7AwMD4wMDAwMAAAAAAAAB+wMDA3sbGxsZ+AAAAAAAAxsbGxv7GxsbGxgAAAAAAAH4YGBgYGBgYGH4AAAAAAAB+GBgYGBgYGBjwAAAAAAAAxsbGzPjMxsbGxgAAAAAAAMDAwMDAwMDAwH4AAAAAAADG7v7WxsbGxsbGAAAAAAAAxsbm5tbWzs7GxgAAAAAAAHzGxsbGxsbGxnwAAAAAAAD8xsbG/MDAwMDAAAAAAAAAfMbGxsbGxtbWfBgMAAAAAPzGxsb8xsbGxsYAAAAAAAB+wMDAfAYGBgb8AAAAAAAA/xgYGBgYGBgYGAAAAAAAAMbGxsbGxsbGxn4AAAAAAADGxsbGxsbGbDgQAAAAAAAAxsbGxsbG1v7uxgAAAAAAAMbGxmw4bMbGxsYAAAAAAADGxsbGfgYGBgb8AAAAAAAA/gYGDBgwYMDA/gAAAAAAPjAwMDAwMDAwMDA+AAAAAMDAYGAwMBgYDAwGBgAAAAB8DAwMDAwMDAwMDHwAAAAAEDhsxgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAD+AAAwGAwAAAAAAAAAAAAAAAAAAAAAAHwGfsbGxn4AAAAAAADAwMD8xsbGxsb8AAAAAAAAAAAAfsDAwMDAfgAAAAAAAAYGBn7GxsbGxn4AAAAAAAAAAAB+xsb+wMB+AAAAAAAAHjAwMHwwMDAwMAAAAAAAAAAAAH7GxsbGxnwGBvwAAADAwMD8xsbGxsbGAAAAAAAAGBgAOBgYGBgYHAAAAAAAABgYABgYGBgYGBgYGHAAAADAwMDM2PDw2MzGAAAAAAAAMDAwMDAwMDAwHAAAAAAAAAAAAOzW1tbWxsYAAAAAAAAAAAD8xsbGxsbGAAAAAAAAAAAAfMbGxsbGfAAAAAAAAAAAAPzGxsbGxvzAwMAAAAAAAAB+xsbGxsZ+BgYGAAAAAAAAfsbAwMDAwAAAAAAAAAAAAH7AwHwGBvwAAAAAAAAwMDB8MDAwMDAeAAAAAAAAAAAAxsbGxsbGfgAAAAAAAAAAAMbGxsZsOBAAAAAAAAAAAADGxtbW1tZuAAAAAAAAAAAAxmw4OGzGxgAAAAAAAAAAAMbGxsbGxn4GBvwAAAAAAAD+BgwYMGD+AAAAAAAOGBgYGHBwGBgYGA4AAAAAGBgYGBgYGBgYGBgYAAAAAHAYGBgYDg4YGBgYcAAAAAAAAAAAADJ+TAAAAAAAAAAAAAAAEDhsxsbG/gAAAAAAAAB+wMDAwMDAwMB+GBgwAAAAbGwAxsbGxsbGfgAAAAAADBgwAH7Gxv7AwH4AAAAAABA4bAB8Bn7GxsZ+AAAAAAAAbGwAfAZ+xsbGfgAAAAAAYDAYAHwGfsbGxn4AAAAAADhsOAB8Bn7GxsZ+AAAAAAAAAAAAfsDAwMDAfhgYMAAAEDhsAH7Gxv7AwH4AAAAAAABsbAB+xsb+wMB+AAAAAABgMBgAfsbG/sDAfgAAAAAAAGZmADgYGBgYGBwAAAAAABg8ZgA4GBgYGBgcAAAAAAAwGAwAOBgYGBgYHAAAAABsbAB8xsbG/sbGxsYAAAAAOGw4fMbGxv7GxsbGAAAAABgwAH7AwMD4wMDAfgAAAAAAAAAAAG4WFn7Q0G4AAAAAAAB+2NjY/tjY2NjeAAAAAAAQOGwAfMbGxsbGfAAAAAAAAGxsAHzGxsbGxnwAAAAAAGAwGAB8xsbGxsZ8AAAAAAAQOGwAxsbGxsbGfgAAAAAAYDAYAMbGxsbGxn4AAAAAAABsbADGxsbGxsZ+Bgb8AGxsAHzGxsbGxsbGfAAAAABsbADGxsbGxsbGxn4AAAAAAAAAAAh+yMjIyMh+CAAAAAAAOGxgYGD4YGDA/gAAAAAAAMPDZjwYPBg8GBgAAAAAAPjMzPjAzN7MzMzGAAAAAAAADhsbGBgYfhgYGNjYcAAADBgwAHwGfsbGxn4AAAAAAAwYMAA4GBgYGBgcAAAAAAAMGDAAfMbGxsbGfAAAAAAADBgwAMbGxsbGxn4AAAAAADJ+TAD8xsbGxsbGAAAAADJMAMbm5tbWzs7GxgAAAAAAOAw8TDwAfAAAAAAAAAAAADhsbDgAAHwAAAAAAAAAAAAAGBgAGBgwYMDGfAAAAAAAAAAAAAD+wMDAAAAAAAAAAAAAAAAA/gYGBgAAAAAAAABAwEBCRuwYMGzSggwQHgAAQMBAQkbsGDBw1JQeBAQAAAAYGAAYGBgYGBgYAAAAAAAAAAAAADNmzGYzAAAAAAAAAAAAAADMZjNmzAAAAAAAEUQRRBFEEUQRRBFEEUQRRFWqVapVqlWqVapVqlWqVardd9133Xfdd9133Xfdd913GBgYGBgYGBgYGBgYGBgYGBgYGBgYGBj4GBgYGBgYGBgYGBgYGBj4GPgYGBgYGBgYNjY2NjY2NvY2NjY2NjY2NgAAAAAAAAD+NjY2NjY2NjYAAAAAAAD4GPgYGBgYGBgYNjY2NjY29gb2NjY2NjY2NjY2NjY2NjY2NjY2NjY2NjYAAAAAAAD+BvY2NjY2NjY2NjY2NjY29gb+AAAAAAAAADY2NjY2Njb+AAAAAAAAAAAYGBgYGBj4GPgAAAAAAAAAAAAAAAAAAPgYGBgYGBgYGBgYGBgYGBgfAAAAAAAAAAAYGBgYGBgY/wAAAAAAAAAAAAAAAAAAAP8YGBgYGBgYGBgYGBgYGBgfGBgYGBgYGBgAAAAAAAAA/wAAAAAAAAAAGBgYGBgYGP8YGBgYGBgYGBgYGBgYGB8YHxgYGBgYGBg2NjY2NjY2NzY2NjY2NjY2NjY2NjY2NzA/AAAAAAAAAAAAAAAAAD8wNzY2NjY2NjY2NjY2Njb3AP8AAAAAAAAAAAAAAAAA/wD3NjY2NjY2NjY2NjY2NjcwNzY2NjY2NjYAAAAAAAD/AP8AAAAAAAAANjY2NjY29wD3NjY2NjY2NhgYGBgYGP8A/wAAAAAAAAA2NjY2NjY2/wAAAAAAAAAAAAAAAAAA/wD/GBgYGBgYGAAAAAAAAAD/NjY2NjY2NjY2NjY2NjY2PwAAAAAAAAAAGBgYGBgYHxgfAAAAAAAAAAAAAAAAAB8YHxgYGBgYGBgAAAAAAAAAPzY2NjY2NjY2NjY2NjY2Nv82NjY2NjY2NhgYGBgYGP8Y/xgYGBgYGBgYGBgYGBgY+AAAAAAAAAAAAAAAAAAAAB8YGBgYGBgYGP////////////////////8AAAAAAAAAAP//////////8PDw8PDw8PDw8PDw8PDw8A8PDw8PDw8PDw8PDw8PDw///////////wAAAAAAAAAAAAAAAAB23NjY2Nx2AAAAAAAAeMzMzNjMxsbW3AAAAAAAAP7GwMDAwMDAwMAAAAAAAAAAAAD+bGxsbGxsAAAAAAAA/sZgMBgYMGDG/gAAAAAAAAAAAH7Y2NjY2HAAAAAAAAAAAADMzMzMzMz2wMDAAAAAAAAAfhgYGBgYDAAAAAAAABB81tbW1tbWfBAAAAAAAAA4bMbG/sbGxmw4AAAAAAAAOGzGxsbGxmxs7gAAAAAAAB4wGAw8ZmZmZjwAAAAAAAAAAAB+29vbfgAAAAAAAAAAAAAATNbW1tbWfBAQAAAAAAAAAHzGwHDAxnwAAAAAAAAAfMbGxsbGxsbGAAAAAAAAAAB+AAB+AAB+AAAAAAAAAAAAABgYfhgYAH4AAAAAAAAAAAAwGAwYMAA8AAAAAAAAAAAADBgwGAwAPAAAAAAAAA4bGxgYGBgYGBgYGBgYGBgYGBgYGBgY2NhwAAAAAAAAAAAYGAB+ABgYAAAAAAAAAAAAADJMADJMAAAAAAAAADhsbDgAAAAAAAAAAAAAAAAAAAAAADg4OAAAAAAAAAAAAAAAAAAAGBgAAAAAAAAAAA8MDAwMDMxsPBwMAAAAAAB4bGxsbGwAAAAAAAAAAAAAOEwMOGB8AAAAAAAAAAAAAAAAAHx8fHx8fHwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP//AQD//wIA//8DAP//BAD//wUA//8GAP//BwD//wgA//8JAP//CgD//wsA//8MAP//DQD//w4A//8PAP//EAD//xEA//8SAP//EwD//xQA//8VAP//FgD//xcA//8YAP//GQD//xoA//8bAP//HAD//x0A//8eAP//HwD//yAA//8hAP//IgD//yMA//8kAP//JQD//yYA//8nAP//KAD//ykA//8qAP//KwD//ywA//8tAP//LgD//y8A//8wAP//MQD//zIA//8zAP//NAD//zUA//82AP//NwD//zgA//85AP//OgD//zsA//88AP//PQD//z4A//8/AP//QAD//0EA//9CAP//QwD//0QA//9FAP//RgD//0cA//9IAP//SQD//0oA//9LAP//TAD//00A//9OAP//TwD//1AA//9RAP//UgD//1MA//9UAP//VQD//1YA//9XAP//WAD//1kA//9aAP//WwD//1wA//9dAP//XgD//18A//9gAP//YQD//2IA//9jAP//ZAD//2UA//9mAP//ZwD//2gA//9pAP//agD//2sA//9sAP//bQD//24A//9vAP//cAD//3EA//9yAP//cwD//3QA//91AP//dgD//3cA//94AP//eQD//3oA//97AP//fAD//30A//9+AP//fwD//4AA//+BAP//ggD//4MA//+EAP//hQD//4YA//+HAP//iAD//4kA//+KAP//iwD//4wA//+NAP//jgD//48A//+QAP//kQD//5IA//+TAP//lAD//5UA//+WAP//lwD//5gA//+ZAP//mgD//5sA//+cAP//nQD//54A//+fAP//oAD//6EA//+iAP//owD//6QA//+lAP//pgD//6cA//+oAP//qQD//6oA//+rAP//rAD//60A//+uAP//rwD//7AA//+xAP//sgD//7MA//+0AP//tQD//7YA//+3AP//uAD//7kA//+6AP//uwD//7wA//+9AP//vgD//78A///AAP//wQD//8IA///DAP//xAD//8UA///GAP//xwD//8gA///JAP//ygD//8sA///MAP//zQD//84A///PAP//0AD//9EA///SAP//0wD//9QA///VAP//1gD//9cA///YAP//2QD//9oA///bAP//3AD//90A///eAP//3wD//+AA///hAP//4gD//+MA///kAP//5QD//+YA///nAP//6AD//+kA///qAP//6wD//+wA///tAP//7gD//+8A///wAP//8QD///IA///zAP//9AD///UA///2AP//9wD///gA///5AP//+gD///sA///8AP///QD///4A////AP//";

    private PCScreenFont LoadUiFont()
    {
        try
        {
            byte[] data = Convert.FromBase64String(UI_FONT_DATA);

            if (data.Length > 4 && data[0] == 0x36 && data[1] == 0x04)
            {
                PCScreenFont font = PCScreenFont.LoadFont(data);

                if (font != null && font.Width == 8 && font.Height == 16)
                    return font;
            }
        }
        catch
        {
        }

        return PCScreenFont.DefaultFont;
    }
}
