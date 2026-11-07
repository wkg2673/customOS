# customOS

<p align="center">

**A tiny hobby operating system written in C# using Cosmos Gen3.**

*Built from curiosity, one line of code at a time.*

</p>

---

## About

customOS is a hobby operating system project developed in C# using [Cosmos Gen3](https://cosmosos.github.io/).

The goal of this project is to explore operating system development, hardware interaction, kernel programming and eventually build a complete operating system with its own command-line interface and graphical environment.

customOS currently features a custom boot screen, an interactive terminal, basic commands, CPU detection and system information.

## Features

- Custom ASCII boot logo.
- Interactive command-line interface.
- Command history.
- Basic calculator.
- CPU name detection using CPUID.
- System information display.
- System uptime tracking.
- Date and time display.
- Reboot functionality.
- Shutdown functionality.

## Screenshots

*Screenshots coming soon!*

## Commands

customOS currently supports the following commands:

| Command | Description |
|---|---|
| `help` | Displays all available commands. |
| `about` | Shows information about customOS and its creator. |
| `clear` | Clears the terminal screen. |
| `echo <text>` | Prints the specified text. |
| `time` | Displays the current system time. |
| `date` | Displays the current date. |
| `uptime` | Shows system uptime. |
| `history` | Displays previously executed commands. |
| `version` | Displays the customOS version. |
| `whoami` | Displays the current username. |
| `commands` | Shows the number of executed commands. |
| `calc <a> <op> <b>` | Performs basic calculations. |
| `sysinfo` | Displays system and processor information. |
| `reboot` | Restarts the computer. |
| `shutdown` | Shuts down the computer. |
| `exit` | Alias for shutdown. |

### Calculator examples

```text
customOS> calc 10 + 5
Result: 15

customOS> calc 100 * 2
Result: 200

customOS> calc 50 / 5
Result: 10
