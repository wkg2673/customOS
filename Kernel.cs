
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using Sys = Cosmos.Kernel.System;

namespace customOS;

public class Kernel : Sys.Kernel
{
    private readonly List<string> commandHistory = new();

    private DateTime bootTime;
    private int commandCount = 0;

    protected override void BeforeRun()
    {
        bootTime = DateTime.Now;

        Console.Clear();

        Console.ForegroundColor = ConsoleColor.Cyan;

        Console.WriteLine(@"  ____           _                 ___  ____");
        Console.WriteLine(@" / ___|   _ ___ | |_ ___  _ __ ___ / _ \/ ___|");
        Console.WriteLine(@"| |  | | | / __|| __/ _ \| '_ ` _ \ | | \___ \");
        Console.WriteLine(@"| |__| |_| \__ \| || (_) | | | | | | |_| |___) |");
        Console.WriteLine(@" \____\__,_|___/ \__\___/|_| |_| |_|\___/|____/");

        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("             customOS v1.0");
        Console.WriteLine();
        Console.WriteLine("       Welcome back, wkg2673!");
        Console.WriteLine();
        Console.WriteLine("       System successfully initialized.");
        Console.WriteLine();
    }

    protected override void Run()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("customOS");
        Console.ResetColor();

        Console.Write("> ");

        string input = Console.ReadLine() ?? "";

        if (string.IsNullOrWhiteSpace(input))
            return;

        commandHistory.Add(input);
        commandCount++;

        string[] parts = input.Split(' ', 2);

        string command = parts[0].ToLower();
        string argument = parts.Length > 1 ? parts[1] : "";

        switch (command)
        {
            case "help":
                Help();
                break;

            case "about":
                About();
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
                Console.WriteLine("customOS version 1.0");
                break;

            case "whoami":
                Console.WriteLine("root");
                break;

            case "commands":
                Console.WriteLine($"Commands executed: {commandCount}");
                break;

            case "calc":
                Calculate(argument);
                break;

            case "sysinfo":
                ShowSystemInfo();
                break;

            case "reboot":
                Reboot();
                break;

            case "exit":
            case "shutdown":
                Shutdown();
                break;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Unknown command: {command}");
                Console.ResetColor();

                Console.WriteLine("Type 'help' for available commands.");
                break;
        }

        Console.WriteLine();
    }

    private void Help()
    {
        Console.WriteLine("Available commands:");
        Console.WriteLine();

        Console.WriteLine("  help       - Show this help menu");
        Console.WriteLine("  about      - Information about customOS");
        Console.WriteLine("  clear      - Clear the screen");
        Console.WriteLine("  echo       - Print text");
        Console.WriteLine("  time       - Show current time");
        Console.WriteLine("  date       - Show current date");
        Console.WriteLine("  uptime     - Show system uptime");
        Console.WriteLine("  history    - Show command history");
        Console.WriteLine("  version    - Show OS version");
        Console.WriteLine("  whoami     - Show current user");
        Console.WriteLine("  commands   - Show command count");
        Console.WriteLine("  calc       - Perform basic calculations");
        Console.WriteLine("  sysinfo    - Show hardware/system information");
        Console.WriteLine("  reboot     - Restart the computer");
        Console.WriteLine("  shutdown   - Shut down the computer");
        Console.WriteLine("  exit       - Shut down customOS");
    }

    private void About()
    {
        Console.WriteLine("customOS");
        Console.WriteLine("Version: 1.0");
        Console.WriteLine("Architecture: Cosmos Gen3");
        Console.WriteLine("Language: C#");
        Console.WriteLine("Kernel: customOS Kernel");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Created by wkg2673");
        Console.ResetColor();
    }

    private void ShowSystemInfo()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("========================================");
        Console.WriteLine("          customOS SYSTEM INFO");
        Console.WriteLine("========================================");
        Console.ResetColor();

        Console.WriteLine();

        // Operating system
        Console.WriteLine("[ OPERATING SYSTEM ]");
        Console.WriteLine("OS Name:          customOS");
        Console.WriteLine("OS Version:       1.0");
        Console.WriteLine("Kernel:           Cosmos Gen3");
        Console.WriteLine("Language:         C# / .NET NativeAOT");

        Console.WriteLine();

        // Processor
        Console.WriteLine("[ PROCESSOR ]");

        Console.WriteLine($"CPU Name:         {GetCpuName()}");
        Console.WriteLine($"Architecture:     {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"Logical CPUs:     {Environment.ProcessorCount}");
        Console.WriteLine($"64-bit Process:   {Environment.Is64BitProcess}");

        Console.WriteLine();

        // Memory
        Console.WriteLine("[ MEMORY ]");

        try
        {
            long managedMemory = GC.GetTotalMemory(false);

            Console.WriteLine(
                $"Managed heap:     {managedMemory / 1024.0 / 1024.0:F2} MB"
            );

            Console.WriteLine("Note: Managed heap is not total physical RAM.");
        }
        catch
        {
            Console.WriteLine("Memory information unavailable.");
        }

        Console.WriteLine();

        // Runtime
        Console.WriteLine("[ RUNTIME ]");
        Console.WriteLine(
            $"Runtime:          {RuntimeInformation.FrameworkDescription}"
        );

        Console.WriteLine();

        // System status
        Console.WriteLine("[ SYSTEM STATUS ]");
        Console.WriteLine($"Uptime:           {DateTime.Now - bootTime}");
        Console.WriteLine($"Commands run:     {commandCount}");

        Console.WriteLine();
        Console.WriteLine("========================================");
    }

    // Detect actual CPU model using the x86 CPUID instruction.
    private string GetCpuName()
    {
        if (!X86Base.IsSupported)
            return "CPU name detection unavailable";

        try
        {
            var maxLeafResult = X86Base.CpuId(unchecked((int)0x80000000), 0);

            uint maxLeaf = unchecked((uint)maxLeafResult.Eax);

            if (maxLeaf < 0x80000004)
                return "CPU brand string not supported";

            StringBuilder cpuName = new StringBuilder();

            for (int leaf = unchecked((int)0x80000002);
                leaf <= unchecked((int)0x80000004);
                leaf++)
            {
                var result = X86Base.CpuId(leaf, 0);

                AppendRegister(cpuName, result.Eax);
                AppendRegister(cpuName, result.Ebx);
                AppendRegister(cpuName, result.Ecx);
                AppendRegister(cpuName, result.Edx);
            }

            string name = cpuName.ToString()
                .Trim('\0', ' ', '\t', '\r', '\n');

            return string.IsNullOrWhiteSpace(name)
                ? "Unknown CPU"
                : name;
        }
        catch
        {
            return "CPU detection failed";
        }
    }

    // Convert a 32-bit CPUID register into four ASCII characters.
    private void AppendRegister(StringBuilder builder, int register)
    {
        for (int i = 0; i < 4; i++)
        {
            char character = (char)((register >> (i * 8)) & 0xFF);
            builder.Append(character);
        }
    }

    private void ShowHistory()
    {
        Console.WriteLine("Command history:");

        for (int i = 0; i < commandHistory.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {commandHistory[i]}");
        }
    }

    private void Calculate(string expression)
    {
        string[] parts = expression.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries
        );

        if (parts.Length != 3)
        {
            Console.WriteLine("Usage: calc <number> <operator> <number>");
            Console.WriteLine("Example: calc 10 + 5");
            return;
        }

        if (!double.TryParse(parts[0], out double a) ||
            !double.TryParse(parts[2], out double b))
        {
            Console.WriteLine("Invalid numbers.");
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

        Console.WriteLine($"Result: {result}");
    }

    private void Reboot()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Restarting customOS...");
        Console.ResetColor();

        Sys.Power.Reboot();

        while (true)
        {
        }
    }

    private void Shutdown()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Shutting down customOS...");
        Console.ResetColor();

        Sys.Power.Shutdown();

        while (true)
        {
        }
    }
}
