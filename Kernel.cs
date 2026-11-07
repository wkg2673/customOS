
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
using Cosmos.Kernel.System.Network;

namespace customOS;

public class Kernel : Sys.Kernel
{
    private DateTime bootTime;
    private int commandCount;

    private readonly List<string> commandHistory = new();

    private string currentDirectory = "/mnt";

    private Canvas? desktop;

    private int screenWidth;
    private int screenHeight;

    private bool guiRunning;
    private bool startMenuOpen;

    private bool previousMouseButton;
    private bool dragging;
    private bool resizing;

    private int dragOffsetX;
    private int dragOffsetY;

    private int windowX = 190;
    private int windowY = 100;
    private int windowWidth = 590;
    private int windowHeight = 390;

    private const int APP_NONE = 0;
    private const int APP_TERMINAL = 1;
    private const int APP_FILES = 2;
    private const int APP_SYSTEM = 3;
    private const int APP_SETTINGS = 4;

    private int activeApp = APP_NONE;

    private bool darkTheme = true;

    private string terminalInput = "";

    private readonly List<string> terminalOutput = new();

    private Color Background =>
        darkTheme
            ? Color.FromArgb(24, 34, 52)
            : Color.FromArgb(100, 145, 190);

    private Color WindowColor =>
        darkTheme
            ? Color.FromArgb(28, 37, 53)
            : Color.FromArgb(245, 248, 252);

    private Color TextColor =>
        darkTheme ? Color.White : Color.Black;

    private Color Accent =>
        Color.FromArgb(40, 125, 220);

    // ==========================================
    // BOOT
    // ==========================================

    protected override void BeforeRun()
    {
        bootTime = DateTime.Now;

        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;

        Console.WriteLine(@"  ____           _             ___  ____");
        Console.WriteLine(@" / ___|   _ ___ | | ___  _ __ / _ \/ ___|");
        Console.WriteLine(@"| |  | | | / __|| __/ _ \| '_ \ | | \___ \");
        Console.WriteLine(@"| |__| |_| \__ \| || (_) | | | | |_| |___) |");
        Console.WriteLine(@" \_____\__,_|___/ \__\___/|_| |_|\___/|____/");

        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("             customOS v3.0");
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
        Console.Write(" [" + currentDirectory + "]");

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
                Console.WriteLine(DateTime.Now - bootTime);
                break;

            case "history":
                ShowHistory();
                break;

            case "version":
                Console.WriteLine("customOS version 3.0");
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
                ShowMemory();
                break;

            case "disks":
                ShowDisks();
                break;

            case "network":
                ShowNetwork();
                break;

            case "ls":
            case "dir":
                ListDirectory(argument);
                break;

            case "cd":
                ChangeDirectory(argument);
                break;

            case "pwd":
                Console.WriteLine(currentDirectory);
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
                DeleteFile(argument);
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

            MouseManager.SetScreenSize(screenWidth, screenHeight);

            windowX = Math.Max(20, screenWidth / 2 - 295);
            windowY = Math.Max(60, screenHeight / 2 - 195);

            activeApp = APP_NONE;
            startMenuOpen = false;
            guiRunning = true;

            terminalOutput.Clear();
            terminalInput = "";

            terminalOutput.Add("customOS Terminal v3.0");
            terminalOutput.Add("Type help for commands.");

            while (guiRunning)
            {
                HandleKeyboard();
                HandleMouse();

                DrawDesktop();

                Thread.Sleep(16);
            }
        }
        catch (Exception ex)
        {
            guiRunning = false;
            Console.WriteLine("GUI error: " + ex.Message);
        }

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

        desktop.Clear(Background);

        DrawWallpaper();
        DrawDesktopIcons();

        if (activeApp != APP_NONE)
            DrawApplicationWindow();

        DrawTaskbar();

        if (startMenuOpen)
            DrawStartMenu();

        DrawMouseCursor();

        desktop.Display();
    }

    private void DrawWallpaper()
    {
        if (desktop == null)
            return;

        desktop.DrawFilledCircle(
            Color.FromArgb(35, 75, 125),
            screenWidth - 100,
            120,
            160
        );

        desktop.DrawFilledCircle(
            Color.FromArgb(30, 60, 100),
            screenWidth - 40,
            170,
            100
        );

        desktop.DrawString(
            "customOS",
            PCScreenFont.DefaultFont,
            Color.White,
            25,
            20
        );

        desktop.DrawString(
            "Desktop environment",
            PCScreenFont.DefaultFont,
            Color.LightGray,
            25,
            42
        );
    }

    // ==========================================
    // DESKTOP ICONS
    // ==========================================

    private void DrawDesktopIcons()
    {
        if (desktop == null)
            return;

        DrawIcon(25, 100, "Computer", Color.SteelBlue);
        DrawIcon(25, 180, "Files", Color.Goldenrod);
        DrawIcon(25, 260, "Terminal", Color.DarkSlateGray);
        DrawIcon(25, 340, "System", Color.DarkGreen);
        DrawIcon(25, 420, "Settings", Color.Gray);
    }

    private void DrawIcon(int x, int y, string name, Color color)
    {
        if (desktop == null)
            return;

        desktop.DrawFilledRectangle(color, x + 12, y, 35, 35);
        desktop.DrawRectangle(Color.White, x + 12, y, 35, 35);

        desktop.DrawString(
            name,
            PCScreenFont.DefaultFont,
            Color.White,
            x,
            y + 43
        );
    }

    // ==========================================
    // TASKBAR
    // ==========================================

    private void DrawTaskbar()
    {
        if (desktop == null)
            return;

        int y = screenHeight - 45;

        desktop.DrawFilledRectangle(
            Color.FromArgb(15, 21, 34),
            0,
            y,
            screenWidth,
            45
        );

        desktop.DrawFilledRectangle(Accent, 5, y + 5, 90, 35);

        desktop.DrawString(
            "START",
            PCScreenFont.DefaultFont,
            Color.White,
            25,
            y + 15
        );

        if (activeApp != APP_NONE)
        {
            desktop.DrawString(
                GetAppName(activeApp),
                PCScreenFont.DefaultFont,
                Color.White,
                115,
                y + 15
            );
        }

        desktop.DrawString(
            DateTime.Now.ToString("HH:mm:ss"),
            PCScreenFont.DefaultFont,
            Color.White,
            screenWidth - 95,
            y + 15
        );
    }

    // ==========================================
    // START MENU
    // ==========================================

    private void DrawStartMenu()
    {
        if (desktop == null)
            return;

        int x = 5;
        int y = screenHeight - 330;

        desktop.DrawFilledRectangle(
            Color.FromArgb(25, 35, 55),
            x, y, 240, 280
        );

        desktop.DrawRectangle(Accent, x, y, 240, 280);

        desktop.DrawString(
            "customOS",
            PCScreenFont.DefaultFont,
            Color.White,
            x + 15, y + 15
        );

        desktop.DrawString(
            "Applications",
            PCScreenFont.DefaultFont,
            Color.LightGray,
            x + 15, y + 45
        );

        desktop.DrawString("1. Terminal", PCScreenFont.DefaultFont,
            Color.White, x + 15, y + 85);

        desktop.DrawString("2. File Manager", PCScreenFont.DefaultFont,
            Color.White, x + 15, y + 120);

        desktop.DrawString("3. System Monitor", PCScreenFont.DefaultFont,
            Color.White, x + 15, y + 155);

        desktop.DrawString("4. Settings", PCScreenFont.DefaultFont,
            Color.White, x + 15, y + 190);

        desktop.DrawString("5. Exit Desktop", PCScreenFont.DefaultFont,
            Color.Orange, x + 15, y + 240);
    }

    // ==========================================
    // APPLICATION WINDOWS
    // ==========================================

    private void DrawApplicationWindow()
    {
        if (desktop == null)
            return;

        desktop.DrawFilledRectangle(
            WindowColor,
            windowX, windowY, windowWidth, windowHeight
        );

        desktop.DrawRectangle(
            Accent,
            windowX, windowY, windowWidth, windowHeight
        );

        desktop.DrawFilledRectangle(
            Accent,
            windowX, windowY, windowWidth, 32
        );

        desktop.DrawString(
            GetAppName(activeApp),
            PCScreenFont.DefaultFont,
            Color.White,
            windowX + 12,
            windowY + 9
        );

        desktop.DrawFilledRectangle(
            Color.DarkRed,
            windowX + windowWidth - 30,
            windowY + 3,
            25,
            25
        );

        desktop.DrawString(
            "X",
            PCScreenFont.DefaultFont,
            Color.White,
            windowX + windowWidth - 22,
            windowY + 9
        );

        desktop.DrawFilledRectangle(
            Accent,
            windowX + windowWidth - 12,
            windowY + windowHeight - 12,
            12,
            12
        );

        switch (activeApp)
        {
            case APP_TERMINAL:
                DrawTerminal();
                break;

            case APP_FILES:
                DrawFileManager();
                break;

            case APP_SYSTEM:
                DrawSystemMonitor();
                break;

            case APP_SETTINGS:
                DrawSettings();
                break;
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
            default: return "Application";
        }
    }

    // ==========================================
    // TERMINAL WINDOW
    // ==========================================

    private void DrawTerminal()
    {
        if (desktop == null)
            return;

        int x = windowX + 12;
        int y = windowY + 48;

        desktop.DrawFilledRectangle(
            Color.FromArgb(12, 17, 25),
            x, y, windowWidth - 24, windowHeight - 90
        );

        int maxLines = (windowHeight - 115) / 18;

        int start = Math.Max(0, terminalOutput.Count - maxLines);

        for (int i = start; i < terminalOutput.Count; i++)
        {
            int lineY = y + 10 + (i - start) * 18;

            desktop.DrawString(
                terminalOutput[i],
                PCScreenFont.DefaultFont,
                Color.FromArgb(190, 225, 195),
                x + 8,
                lineY
            );
        }

        int inputY = windowY + windowHeight - 32;

        desktop.DrawString(
            "> " + terminalInput + "_",
            PCScreenFont.DefaultFont,
            TextColor,
            x + 5,
            inputY
        );
    }

    // ==========================================
    // FILE MANAGER
    // ==========================================

    private void DrawFileManager()
    {
        if (desktop == null)
            return;

        int x = windowX + 18;
        int y = windowY + 48;

        desktop.DrawString(
            "Location: " + currentDirectory,
            PCScreenFont.DefaultFont,
            TextColor,
            x, y
        );

        desktop.DrawLine(
            Color.Gray,
            x, y + 22,
            windowX + windowWidth - 15, y + 22
        );

        try
        {
            int row = 0;

            foreach (string directory in Directory.GetDirectories(currentDirectory))
            {
                if (row >= 12) break;

                desktop.DrawString(
                    "[DIR] " + directory,
                    PCScreenFont.DefaultFont,
                    Accent,
                    x, y + 35 + row * 22
                );

                row++;
            }

            foreach (string file in Directory.GetFiles(currentDirectory))
            {
                if (row >= 12) break;

                desktop.DrawString(
                    "[FILE] " + file,
                    PCScreenFont.DefaultFont,
                    TextColor,
                    x, y + 35 + row * 22
                );

                row++;
            }

            if (row == 0)
            {
                desktop.DrawString(
                    "This directory is empty.",
                    PCScreenFont.DefaultFont,
                    TextColor,
                    x, y + 45
                );
            }
        }
        catch
        {
            desktop.DrawString(
                "Filesystem unavailable.",
                PCScreenFont.DefaultFont,
                Color.OrangeRed,
                x, y + 45
            );
        }

        desktop.DrawString(
            "Use terminal commands to manage files.",
            PCScreenFont.DefaultFont,
            Color.Gray,
            x,
            windowY + windowHeight - 30
        );
    }

    // ==========================================
    // SYSTEM MONITOR
    // ==========================================

    private void DrawSystemMonitor()
    {
        if (desktop == null)
            return;

        int x = windowX + 20;
        int y = windowY + 55;

        desktop.DrawString(
            "SYSTEM INFORMATION",
            PCScreenFont.DefaultFont,
            Accent,
            x, y
        );

        desktop.DrawString(
            "Operating System: customOS 3.0",
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 35
        );

        desktop.DrawString(
            "Kernel: Cosmos Gen3",
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 65
        );

        desktop.DrawString(
            "Architecture: " + RuntimeInformation.ProcessArchitecture,
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 95
        );

        desktop.DrawString(
            "CPU: " + GetCpuName(),
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 125
        );

        desktop.DrawString(
            "Logical CPUs: " + Environment.ProcessorCount,
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 155
        );

        desktop.DrawString(
            "Managed heap: " +
            (GC.GetTotalMemory(false) / 1048576.0).ToString("F2") + " MB",
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 185
        );

        desktop.DrawString(
            "Physical RAM: Not exposed by current kernel API",
            PCScreenFont.DefaultFont,
            Color.Orange,
            x, y + 215
        );

        desktop.DrawString(
            "Storage devices: " + StorageManager.Devices.Count,
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 245
        );

        desktop.DrawString(
            "Network adapters: " + NetworkManager.DeviceCount,
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 275
        );
    }

    // ==========================================
    // SETTINGS
    // ==========================================

    private void DrawSettings()
    {
        if (desktop == null)
            return;

        int x = windowX + 20;
        int y = windowY + 60;

        desktop.DrawString(
            "DESKTOP SETTINGS",
            PCScreenFont.DefaultFont,
            Accent,
            x, y
        );

        desktop.DrawFilledRectangle(
            Accent,
            x, y + 45, 210, 40
        );

        desktop.DrawString(
            darkTheme ? "Switch to Light Theme" : "Switch to Dark Theme",
            PCScreenFont.DefaultFont,
            Color.White,
            x + 10, y + 58
        );

        desktop.DrawString(
            "Display resolution: " + screenWidth + " x " + screenHeight,
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 110
        );

        desktop.DrawString(
            "Graphics: Cosmos Graphics Subsystem",
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 145
        );

        desktop.DrawString(
            "Input: Keyboard and Mouse",
            PCScreenFont.DefaultFont,
            TextColor,
            x, y + 180
        );
    }

    // ==========================================
    // MOUSE INPUT
    // ==========================================

    private void HandleMouse()
    {
        int mx = MouseManager.X;
        int my = MouseManager.Y;

        bool pressed = MouseManager.LeftButton;

        bool clicked = pressed && !previousMouseButton;
        bool released = !pressed && previousMouseButton;

        if (released)
        {
            dragging = false;
            resizing = false;
        }

        if (pressed && dragging)
        {
            windowX = Math.Clamp(mx - dragOffsetX, 0, screenWidth - 100);
            windowY = Math.Clamp(my - dragOffsetY, 0, screenHeight - 60);
        }

        if (pressed && resizing)
        {
            windowWidth = Math.Clamp(
                mx - windowX, 300, screenWidth - windowX
            );

            windowHeight = Math.Clamp(
                my - windowY, 180, screenHeight - windowY - 45
            );
        }

        if (clicked)
        {
            int taskbarY = screenHeight - 45;

            if (my >= taskbarY && mx < 100)
            {
                startMenuOpen = !startMenuOpen;
                dragging = false;
            }
            else if (startMenuOpen &&
                     mx >= 5 && mx <= 245 &&
                     my >= screenHeight - 330 &&
                     my <= screenHeight - 50)
            {
                int relativeY = my - (screenHeight - 330);

                if (relativeY >= 75 && relativeY < 110)
                    OpenApplication(APP_TERMINAL);
                else if (relativeY >= 110 && relativeY < 145)
                    OpenApplication(APP_FILES);
                else if (relativeY >= 145 && relativeY < 180)
                    OpenApplication(APP_SYSTEM);
                else if (relativeY >= 180 && relativeY < 215)
                    OpenApplication(APP_SETTINGS);
                else if (relativeY >= 220)
                    guiRunning = false;

                startMenuOpen = false;
            }
            else if (!startMenuOpen && my >= 90 && my < 480 && mx < 100)
            {
                int icon = (my - 90) / 80;

                switch (icon)
                {
                    case 0: OpenApplication(APP_SYSTEM); break;
                    case 1: OpenApplication(APP_FILES); break;
                    case 2: OpenApplication(APP_TERMINAL); break;
                    case 3: OpenApplication(APP_SYSTEM); break;
                    case 4: OpenApplication(APP_SETTINGS); break;
                }
            }
            else if (activeApp != APP_NONE &&
                     mx >= windowX && mx <= windowX + windowWidth &&
                     my >= windowY && my <= windowY + windowHeight)
            {
                if (mx >= windowX + windowWidth - 32 &&
                    my <= windowY + 32)
                {
                    activeApp = APP_NONE;
                    dragging = false;
                }
                else if (mx >= windowX + windowWidth - 20 &&
                         my >= windowY + windowHeight - 20)
                {
                    resizing = true;
                }
                else if (my <= windowY + 32)
                {
                    dragging = true;
                    dragOffsetX = mx - windowX;
                    dragOffsetY = my - windowY;
                }
                else if (activeApp == APP_SETTINGS)
                {
                    int sx = windowX + 20;
                    int sy = windowY + 105;

                    if (mx >= sx && mx <= sx + 210 &&
                        my >= sy && my <= sy + 40)
                    {
                        darkTheme = !darkTheme;
                    }
                }
            }
            else
            {
                startMenuOpen = false;
            }
        }

        previousMouseButton = pressed;
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

            if (key.Key == Key.Escape)
            {
                if (startMenuOpen)
                    startMenuOpen = false;
                else if (activeApp != APP_NONE)
                    activeApp = APP_NONE;
                else
                    guiRunning = false;

                continue;
            }

            if (activeApp == APP_TERMINAL)
                HandleTerminalKey(key);

            if (activeApp == APP_NONE)
            {
                if (key.Key == Key.F1)
                    startMenuOpen = !startMenuOpen;

                if (key.Key == Key.D1)
                    OpenApplication(APP_TERMINAL);

                if (key.Key == Key.D2)
                    OpenApplication(APP_FILES);

                if (key.Key == Key.D3)
                    OpenApplication(APP_SYSTEM);

                if (key.Key == Key.D4)
                    OpenApplication(APP_SETTINGS);
            }
        }
    }

    private void HandleTerminalKey(KeyEvent key)
    {
        if (key.Key == Key.Enter)
        {
            string command = terminalInput.Trim();

            if (command.Length > 0)
            {
                terminalOutput.Add("> " + command);
                ProcessGuiCommand(command);
            }

            terminalInput = "";

            if (terminalOutput.Count > 100)
                terminalOutput.RemoveAt(0);

            return;
        }

        if (key.Key == Key.Backspace)
        {
            if (terminalInput.Length > 0)
                terminalInput = terminalInput.Substring(
                    0, terminalInput.Length - 1
                );

            return;
        }

        char character = key.KeyChar;

        if (character >= 32 && character <= 126 &&
            terminalInput.Length < 70)
        {
            terminalInput += character;
        }
    }

    // ==========================================
    // GUI TERMINAL COMMANDS
    // ==========================================

    private void ProcessGuiCommand(string input)
    {
        string[] parts = input.Split(' ', 2);

        string command = parts[0].ToLowerInvariant();
        string argument = parts.Length > 1 ? parts[1] : "";

        switch (command)
        {
            case "help":
                terminalOutput.Add("help, clear, echo, time, date, uptime");
                terminalOutput.Add("version, whoami, pwd, ls, cd, cat");
                terminalOutput.Add("memory, disks, network, sysinfo");
                terminalOutput.Add("exitgui");
                break;

            case "clear":
                terminalOutput.Clear();
                break;

            case "echo":
                terminalOutput.Add(argument);
                break;

            case "time":
                terminalOutput.Add(DateTime.Now.ToString("HH:mm:ss"));
                break;

            case "date":
                terminalOutput.Add(DateTime.Now.ToString("yyyy-MM-dd"));
                break;

            case "uptime":
                terminalOutput.Add((DateTime.Now - bootTime).ToString());
                break;

            case "version":
                terminalOutput.Add("customOS 3.0");
                break;

            case "whoami":
                terminalOutput.Add("root");
                break;

            case "pwd":
                terminalOutput.Add(currentDirectory);
                break;

            case "ls":
            case "dir":
                try
                {
                    foreach (string directory in Directory.GetDirectories(currentDirectory))
                        terminalOutput.Add("[DIR] " + directory);

                    foreach (string file in Directory.GetFiles(currentDirectory))
                        terminalOutput.Add("[FILE] " + file);
                }
                catch
                {
                    terminalOutput.Add("Cannot access directory.");
                }
                break;

            case "cd":
                ChangeDirectory(argument);
                terminalOutput.Add(currentDirectory);
                break;

            case "cat":
                try
                {
                    string path = ResolvePath(argument);

                    if (File.Exists(path))
                        terminalOutput.Add(File.ReadAllText(path));
                    else
                        terminalOutput.Add("File not found.");
                }
                catch
                {
                    terminalOutput.Add("Cannot read file.");
                }
                break;

            case "memory":
                terminalOutput.Add(
                    "Managed heap: " +
                    (GC.GetTotalMemory(false) / 1048576.0).ToString("F2") +
                    " MB"
                );

                terminalOutput.Add("Physical RAM: unavailable through current API.");
                break;

            case "disks":
                terminalOutput.Add(
                    "Storage devices: " + StorageManager.Devices.Count
                );

                for (int i = 0; i < StorageManager.Devices.Count; i++)
                    terminalOutput.Add(StorageManager.Devices[i].Name);

                break;

            case "network":
                terminalOutput.Add(
                    "Network adapters: " + NetworkManager.DeviceCount
                );

                terminalOutput.Add("Ready: " + NetworkManager.Ready);

                if (NetworkManager.DeviceCount > 0)
                {
                    terminalOutput.Add("Adapter: " + NetworkManager.Name);
                    terminalOutput.Add("Link: " + NetworkManager.LinkUp);
                    terminalOutput.Add("MAC: " + NetworkManager.MacAddress);
                }
                break;

            case "sysinfo":
                terminalOutput.Add("customOS 3.0");
                terminalOutput.Add("CPU: " + GetCpuName());
                terminalOutput.Add("Architecture: " +
                    RuntimeInformation.ProcessArchitecture);
                terminalOutput.Add("Storage: " +
                    StorageManager.Devices.Count);
                terminalOutput.Add("Network: " +
                    NetworkManager.DeviceCount);
                break;

            case "exitgui":
                activeApp = APP_NONE;
                break;

            default:
                terminalOutput.Add("Unknown command: " + command);
                break;
        }
    }

    // ==========================================
    // APPLICATION LAUNCHER
    // ==========================================

    private void OpenApplication(int application)
    {
        activeApp = application;
        startMenuOpen = false;

        dragging = false;
        resizing = false;

        windowWidth = Math.Min(650, screenWidth - 30);
        windowHeight = Math.Min(430, screenHeight - 80);

        windowX = (screenWidth - windowWidth) / 2;
        windowY = (screenHeight - windowHeight) / 2;

        if (activeApp == APP_TERMINAL && terminalOutput.Count == 0)
        {
            terminalOutput.Add("customOS Terminal v3.0");
            terminalOutput.Add("Type help for commands.");
        }
    }

    // ==========================================
    // FILESYSTEM
    // ==========================================

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return currentDirectory;

        if (path.StartsWith("/"))
            return path;

        return currentDirectory.TrimEnd('/') + "/" + path;
    }

    private void ListDirectory(string path)
    {
        string target = ResolvePath(path);

        try
        {
            if (!Directory.Exists(target))
            {
                Console.WriteLine("Directory not found.");
                return;
            }

            foreach (string directory in Directory.GetDirectories(target))
                Console.WriteLine("[DIR] " + directory);

            foreach (string file in Directory.GetFiles(target))
                Console.WriteLine("[FILE] " + file);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Filesystem error: " + ex.Message);
        }
    }

    private void ChangeDirectory(string path)
    {
        string target;

        if (string.IsNullOrWhiteSpace(path))
            target = "/mnt";
        else if (path == "..")
        {
            int index = currentDirectory.LastIndexOf('/');

            target = index <= 0
                ? "/"
                : currentDirectory.Substring(0, index);
        }
        else
            target = ResolvePath(path);

        try
        {
            if (Directory.Exists(target))
                currentDirectory = target;
            else
                Console.WriteLine("Directory not found.");
        }
        catch
        {
            Console.WriteLine("Filesystem unavailable.");
        }
    }

    private void ReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("Usage: cat filename");
            return;
        }

        try
        {
            string target = ResolvePath(path);

            if (File.Exists(target))
                Console.WriteLine(File.ReadAllText(target));
            else
                Console.WriteLine("File not found.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Read error: " + ex.Message);
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

        string path = ResolvePath(argument.Substring(0, separator));
        string content = argument.Substring(separator + 1);

        try
        {
            File.WriteAllText(path, content);
            Console.WriteLine("File saved.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Write error: " + ex.Message);
        }
    }

    private void CreateDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(ResolvePath(path));
            Console.WriteLine("Directory created.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    private void DeleteFile(string path)
    {
        try
        {
            string target = ResolvePath(path);

            if (File.Exists(target))
            {
                File.Delete(target);
                Console.WriteLine("File deleted.");
            }
            else
                Console.WriteLine("File not found.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Delete error: " + ex.Message);
        }
    }

    // ==========================================
    // HARDWARE INFORMATION
    // ==========================================

    private void ShowMemory()
    {
        long heap = GC.GetTotalMemory(false);

        Console.WriteLine(
            "Managed heap: " +
            (heap / 1048576.0).ToString("F2") + " MB"
        );

        Console.WriteLine(
            "Physical RAM: Not exposed through the current API."
        );
    }

    private void ShowDisks()
    {
        Console.WriteLine("Storage devices: " + StorageManager.Devices.Count);

        for (int i = 0; i < StorageManager.Devices.Count; i++)
            Console.WriteLine("[" + i + "] " + StorageManager.Devices[i].Name);

        Console.WriteLine("Partitions: " + StorageManager.Partitions.Count);
    }

    private void ShowNetwork()
    {
        Console.WriteLine("Network adapters: " + NetworkManager.DeviceCount);
        Console.WriteLine("Network ready: " + NetworkManager.Ready);

        if (NetworkManager.DeviceCount > 0)
        {
            Console.WriteLine("Name: " + NetworkManager.Name);
            Console.WriteLine("MAC: " + NetworkManager.MacAddress);
            Console.WriteLine("Link: " + NetworkManager.LinkUp);
        }
    }

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

            StringBuilder builder = new();

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

    private void ShowSystemInfo()
    {
        Console.WriteLine("========== customOS SYSTEM INFO ==========");
        Console.WriteLine("Version: 3.0");
        Console.WriteLine("Kernel: Cosmos Gen3");
        Console.WriteLine("CPU: " + GetCpuName());
        Console.WriteLine("Architecture: " + RuntimeInformation.ProcessArchitecture);
        Console.WriteLine("Logical CPUs: " + Environment.ProcessorCount);

        ShowMemory();
        ShowDisks();
        ShowNetwork();

        Console.WriteLine("Uptime: " + (DateTime.Now - bootTime));
        Console.WriteLine("Commands: " + commandCount);
    }

    // ==========================================
    // TERMINAL UTILITIES
    // ==========================================

    private void Help()
    {
        Console.WriteLine("customOS commands:");
        Console.WriteLine("help       - Show commands");
        Console.WriteLine("about      - About customOS");
        Console.WriteLine("gui        - Open graphical desktop");
        Console.WriteLine("clear      - Clear terminal");
        Console.WriteLine("echo       - Print text");
        Console.WriteLine("time       - Current time");
        Console.WriteLine("date       - Current date");
        Console.WriteLine("uptime     - System uptime");
        Console.WriteLine("history    - Command history");
        Console.WriteLine("version    - OS version");
        Console.WriteLine("whoami     - Current user");
        Console.WriteLine("commands   - Command counter");
        Console.WriteLine("calc       - Calculator");
        Console.WriteLine("sysinfo    - System information");
        Console.WriteLine("memory     - Memory information");
        Console.WriteLine("disks      - Storage devices");
        Console.WriteLine("network    - Network information");
        Console.WriteLine("ls         - List files");
        Console.WriteLine("cd         - Change directory");
        Console.WriteLine("pwd        - Current directory");
        Console.WriteLine("cat        - Read a file");
        Console.WriteLine("write      - Write a file");
        Console.WriteLine("mkdir      - Create directory");
        Console.WriteLine("rm         - Delete a file");
        Console.WriteLine("reboot     - Restart");
        Console.WriteLine("shutdown   - Shut down");
    }

    private void About()
    {
        Console.WriteLine("customOS");
        Console.WriteLine("Version: 3.0");
        Console.WriteLine("Architecture: Cosmos Gen3 x64");
        Console.WriteLine("Language: C#");
        Console.WriteLine("Runtime: .NET 10");
        Console.WriteLine("Creator: wkg2673");
    }

    private void ShowHistory()
    {
        for (int i = 0; i < commandHistory.Count; i++)
            Console.WriteLine((i + 1) + ". " + commandHistory[i]);
    }

    private void Calculate(string expression)
    {
        string[] parts = expression.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
        );

        if (parts.Length != 3 ||
            !double.TryParse(parts[0], out double a) ||
            !double.TryParse(parts[2], out double b))
        {
            Console.WriteLine("Usage: calc 10 + 5");
            return;
        }

        double result;

        switch (parts[1])
        {
            case "+":
                result = a + b;
                break;

            case "-":
                result = a - b;
                break;

            case "*":
                result = a * b;
                break;

            case "/":
                if (b == 0)
                {
                    Console.WriteLine("Cannot divide by zero.");
                    return;
                }

                result = a / b;
                break;

            default:
                Console.WriteLine("Unknown operator.");
                return;
        }

        Console.WriteLine("Result: " + result);
    }

    // ==========================================
    // MOUSE CURSOR
    // ==========================================

    private void DrawMouseCursor()
    {
        if (desktop == null)
            return;

        int x = MouseManager.X;
        int y = MouseManager.Y;

        desktop.DrawLine(Color.Black, x, y, x + 12, y + 16);
        desktop.DrawLine(Color.Black, x + 12, y + 16, x + 5, y + 14);
        desktop.DrawLine(Color.Black, x + 5, y + 14, x + 3, y + 21);
        desktop.DrawLine(Color.White, x, y, x + 10, y + 15);
        desktop.DrawLine(Color.White, x + 10, y + 15, x + 4, y + 13);
    }

    // ==========================================
    // POWER MANAGEMENT
    // ==========================================

    private void Reboot()
    {
        Console.WriteLine("Restarting customOS...");
        Sys.Power.Reboot();

        while (true) { }
    }

    private void Shutdown()
    {
        Console.WriteLine("Shutting down customOS...");
        Sys.Power.Shutdown();

        while (true) { }
    }
}
