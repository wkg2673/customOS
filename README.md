<!-- Links-->
[change_log]: CHANGELOG.md

# customOS

<p align="center">

**A tiny hobby operating system written in C# using Cosmos Gen3.**

</p>
<img width="464" height="291" alt="image" src="https://github.com/user-attachments/assets/d9f39d40-0faa-4344-85bf-8e937929c20c" />
<img width="464" height="291" alt="image" src="https://github.com/user-attachments/assets/7beeac3c-82ef-4f09-9a9e-f30c84437c5c" />



---

## About

customOS is a hobby operating system project developed in C# using [Cosmos Gen3](https://cosmosos.github.io/).

The goal of this project is to explore operating system development, hardware interaction, kernel programming and eventually build a complete operating system with its own command-line interface and graphical environment.

customOS currently features a custom boot screen, an interactive terminal, basic commands, CPU detection, system information and a simple GUI dekstop.

See the [Changelog][change_log] for the newest features.

## Features

- Graphical desktop(early development).
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
- Windows key toggles the Start menu.

## Screenshots

*Screenshots coming soon!*

## Commands

customOS currently supports the following commands:

| Command | Description |
|---|---|
| `help` | Displays all available commands. |
| `about` | Shows information about customOS and its creator. |
| `gui` | Launches into the Graphical Desktop. |
| `clear` | Clears the terminal screen. |
| `echo <text>` | Prints the specified text. |
| `time` | Displays the current system time. |
| `date` | Displays the current date. |
| `uptime` | Shows how long customOS has been running. |
| `history` | Displays previously executed commands. |
| `version` | Displays the customOS version. |
| `whoami` | Displays the current username. |
| `commands` | Shows the number of executed commands. |
| `calc <a> <op> <b>` | Performs basic calculations (`+`, `-`, `*`, `/`). |
| `sysinfo` | Displays system and processor information. |
| `reboot` | Restarts the computer. |
| `shutdown` | Shuts down the computer. |
| `exit` | Alias for `shutdown`. |
