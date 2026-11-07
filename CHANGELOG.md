<!-- List of versions-->
[1.1.0]: https://github.com/wkg2673/customOS/releases/tag/v1.1.0
[1.0.0]: https://github.com/wkg2673/customOS/releases/tag/v1.0.0

# Changelog

All notable changes to customOS are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.1.0] - 2026-11-07

### Added
- Initial graphical user interface (GUI).
- Graphical desktop environment.
- Desktop taskbar and Start menu.
- Mouse input support for desktop interaction.
- Draggable and resizable application windows.
- Built-in graphical applications:
  - Terminal
  - File Manager
  - System Monitor
  - Settings
- Graphical terminal with command input and execution.
- Windows key toggles the Start menu.
- Basic storage device and partition information support.
- Basic network adapter information support.

### Improved
- Expanded customOS beyond its original command-line-only interface.
- Added the foundation for a traditional desktop operating system experience.
- Improved system information capabilities.
- Expanded the way users interact with customOS through both graphical and command-line interfaces.

### Technical Details
- Continued development in C#.
- Built using Cosmos Gen3.
- Targets x86-64 architecture.
- Uses .NET 10 SDK.
- Supports execution in QEMU.

### Known Limitations
- The graphical desktop is still in its early stages.
- Window management and multitasking need further improvements.
- Built-in graphical applications have limited functionality.
- Filesystem support is incomplete.
- Physical RAM detection and reporting are not yet fully implemented.
- Networking is currently limited to basic adapter information.
- Limited hardware and driver support.
- Hardware compatibility is not guaranteed.

### Planned Improvements
- Improve the desktop layout, appearance, and overall user experience.
- Add desktop icons, shortcuts, and context menus.
- Improve window management with minimize, maximize, and close functionality.
- Add better window focus and multitasking support.
- Improve the Start menu and taskbar.
- Add desktop wallpaper and customization options.
- Expand File Manager functionality and filesystem support.
- Improve System Monitor with additional hardware information.
- Expand Settings with configurable system options.
- Add physical RAM detection and reporting.
- Expand networking capabilities.
- Improve hardware compatibility, drivers, and system stability.

---

## [1.0.0] - 2026-11-07

### Added
- Initial release of customOS.
- Custom ASCII boot logo and startup screen.
- Interactive command-line interface.
- Welcome message displaying the creator's username.
- Command history.
- Basic calculator supporting addition, subtraction, multiplication and division.
- CPU name detection using the x86 CPUID instruction.
- System information command (`sysinfo`).
- System uptime tracking.
- Date and time commands.
- Command execution counter.
- Basic user identification using `whoami`.
- Reboot functionality.
- Shutdown functionality.
- Help menu listing all available commands.
- About command displaying OS information.

### Technical Details
- Written in C#.
- Built using Cosmos Gen3.
- Targets x86-64 architecture.
- Uses .NET 10 SDK.
- Supports execution in QEMU.

### Known Limitations
- Limited hardware and driver support.
- No filesystem implementation yet.
- No graphical user interface.
- No networking support.
- Memory reporting currently displays managed heap usage rather than total physical RAM.
- Hardware compatibility is not guaranteed.
