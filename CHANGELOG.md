# Changelog

All notable changes to customOS are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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

---

[1.0.0]: https://github.com/wkg2673/customOS/releases/tag/v1.0.0
