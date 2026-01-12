# Daily Uptime Widget

A smart, premium-designed widget to track your daily work hours on Windows.

## Features

- **Smart Time Tracking**: 
    - Automatically detects system boot time.
    - **8:00 AM Rule**: If you start early (< 8:00 AM), time counts from 08:00. If you start late, it counts from boot time.
- **Goal Notifications**: Notifies you when you reach **8 hours** of work.
- **Intuitive Display**: Shows **Remaining Time** and predicted **End Time** (accounting for lunch break).
- **Auto-Positioning**: Automatically snaps to the Top-Right corner of your screen.
- **System Tray**: Minimizes to tray to keep your desktop clean.

## Installation

1. Download or build the `DailyUptimeWidget.exe`.
2. Run the executable.
3. (Optional) Right-click the widget -> "Auto Start on Boot" to run automatically with Windows.

## Development

- **Tech Stack**: WPF (.NET 8)
- **Architecture**: MVVM
- **Services**: BootTimeService (WMI/EventLog), StartupRegistry, TrayService.

## Author
Developed by Antigravity (~10+ years exp).
