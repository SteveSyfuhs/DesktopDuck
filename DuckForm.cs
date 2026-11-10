using System.Diagnostics;
using System.Media;
using Microsoft.Win32;

namespace DesktopDuck;

internal sealed class DuckForm : DuckWindow
{
    private const string StartupRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryName = "DesktopDuck";
    private readonly DuckSettings settings = DuckSettings.Load();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 25 };
    private readonly FlockMotion flock = new();
    private readonly List<(DuckWindow Window, DucklingMotion Motion)> ducklings = [];
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon tray;
    private readonly Icon duckIcon = DuckArt.CreateIcon();
    private Stream soundStream;
    private readonly SoundPlayer sound;
    private readonly ToolStripMenuItem pauseItem = new("Pause waddling");
    private readonly ToolStripMenuItem soundItem = new("Quack sounds");
    private readonly ToolStripMenuItem taskbarItem = new("Avoid the taskbar");
    private readonly ToolStripMenuItem screenMenu = new("Screen");
    private readonly ToolStripMenuItem startupItem = new("Run on startup");
    private Rectangle screenBounds;
    private double lastTick;
    private bool paused;
    private bool soundFailed;

    public DuckForm()
    {
        Text = "Desktop Duck";
        Icon = duckIcon;
        soundStream = QuackSound.OpenStream(settings.VolumeDb);
        sound = new SoundPlayer(soundStream);

        BuildMenu();
        ContextMenuStrip = menu;
        tray = new NotifyIcon
        {
            Icon = duckIcon,
            Text = "Desktop Duck - right-click for options",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => Quack();
        MouseClick += DuckClicked;
        foreach (DucklingMotion motion in flock.Ducklings)
        {
            var duckling = new DuckWindow
            {
                Text = $"Desktop Duckling {ducklings.Count + 1}",
                ContextMenuStrip = menu
            };
            duckling.MouseClick += DuckClicked;
            ducklings.Add((duckling, motion));
        }
        timer.Tick += (_, _) => TickDuck();
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += UserPreferenceChanged;
        UpdateScreen();
        timer.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        foreach (var duckling in ducklings)
        {
            duckling.Window.Show(this);
        }
    }

    private void DuckClicked(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Quack();
        }
    }

    private void BuildMenu()
    {
        menu.Items.Add(new ToolStripMenuItem("Desktop Duck") { Enabled = false });
        menu.Items.Add("Quack now!", null, (_, _) => Quack());
        pauseItem.CheckOnClick = true;
        pauseItem.Click += (_, _) =>
        {
            paused = pauseItem.Checked;
            pauseItem.Text = paused ? "Resume waddling" : "Pause waddling";
        };
        menu.Items.Add(pauseItem);

        soundItem.Checked = settings.SoundEnabled;
        soundItem.CheckOnClick = true;
        soundItem.Click += (_, _) =>
        {
            settings.SoundEnabled = soundItem.Checked;
            if (!settings.SoundEnabled)
            {
                sound.Stop();
            }
            settings.Save();
        };
        menu.Items.Add(soundItem);

        var volumeMenu = new ToolStripMenuItem("Quack volume");
        AddChoice(volumeMenu, "Full (original level)", 0, () => settings.VolumeDb, SetVolume);
        AddChoice(volumeMenu, "Softer (-3 dB)", -3, () => settings.VolumeDb, SetVolume);
        AddChoice(volumeMenu, "Barely audible (-30 dB)", -30, () => settings.VolumeDb, SetVolume);
        menu.Items.Add(volumeMenu);
        menu.Items.Add(new ToolStripSeparator());

        var speedMenu = new ToolStripMenuItem("Waddle speed");
        AddChoice(speedMenu, "Sleepy", 16, () => settings.Speed, value => settings.Speed = value);
        AddChoice(speedMenu, "Leisurely", 28, () => settings.Speed, value => settings.Speed = value);
        AddChoice(speedMenu, "Places to be", 48, () => settings.Speed, value => settings.Speed = value);
        menu.Items.Add(speedMenu);

        var sizeMenu = new ToolStripMenuItem("Duck size");
        AddChoice(sizeMenu, "Tiny", 24, () => settings.Size, value => settings.Size = value);
        AddChoice(sizeMenu, "Small", 32, () => settings.Size, value => settings.Size = value);
        AddChoice(sizeMenu, "Chonky", 48, () => settings.Size, value => settings.Size = value);
        menu.Items.Add(sizeMenu);

        taskbarItem.Checked = settings.AvoidTaskbar;
        taskbarItem.CheckOnClick = true;
        taskbarItem.Click += (_, _) =>
        {
            settings.AvoidTaskbar = taskbarItem.Checked;
            UpdateScreen();
            settings.Save();
        };
        menu.Items.Add(taskbarItem);
        menu.Items.Add(screenMenu);
        menu.Opening += (_, _) =>
        {
            PopulateScreens();
            RefreshStartupItem();
        };
        menu.Items.Add(new ToolStripSeparator());
        startupItem.Click += (_, _) => ToggleStartup();
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit duck", null, (_, _) => Close());
    }

    private void RefreshStartupItem()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath);
            startupItem.Checked = key?.GetValue(StartupRegistryName) is string command
                && string.Equals(command, $"\"{Application.ExecutablePath}\"", StringComparison.OrdinalIgnoreCase);
            startupItem.Enabled = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            startupItem.Enabled = false;
            MessageBox.Show($"Couldn't read Desktop Duck's startup option.\n\n{exception.Message}",
                "Desktop Duck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ToggleStartup()
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupRegistryPath);
            if (startupItem.Checked)
            {
                key.DeleteValue(StartupRegistryName, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(StartupRegistryName, $"\"{Application.ExecutablePath}\"", RegistryValueKind.String);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            MessageBox.Show($"Couldn't change Desktop Duck's startup option.\n\n{exception.Message}",
                "Desktop Duck", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        RefreshStartupItem();
    }

    private void AddChoice(ToolStripMenuItem parent, string label, int value,
        Func<int> getValue, Action<int> setValue)
    {
        var item = new ToolStripMenuItem(label) { Checked = getValue() == value };
        item.Click += (_, _) =>
        {
            setValue(value);
            foreach (ToolStripMenuItem sibling in parent.DropDownItems)
            {
                sibling.Checked = sibling == item;
            }
            UpdateScreen();
            settings.Save();
        };
        parent.DropDownOpening += (_, _) => item.Checked = getValue() == value;
        parent.DropDownItems.Add(item);
    }

    private void SetVolume(int volumeDb)
    {
        Stream replacement = QuackSound.OpenStream(volumeDb);
        sound.Stop();
        sound.Stream = replacement;
        soundStream.Dispose();
        soundStream = replacement;
        settings.VolumeDb = volumeDb;
    }

    private void PopulateScreens()
    {
        while (screenMenu.DropDownItems.Count > 0)
        {
            screenMenu.DropDownItems[0].Dispose();
        }

        Screen current = SelectedScreen();
        foreach (Screen screen in Screen.AllScreens)
        {
            var item = new ToolStripMenuItem(
                $"{screen.DeviceName}{(screen.Primary ? " (primary)" : "")}")
            {
                Checked = screen.DeviceName == current.DeviceName
            };
            item.Click += (_, _) =>
            {
                settings.ScreenName = screen.DeviceName;
                UpdateScreen();
                settings.Save();
            };
            screenMenu.DropDownItems.Add(item);
        }
    }

    private Screen SelectedScreen() =>
        Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == settings.ScreenName)
        ?? Screen.PrimaryScreen
        ?? Screen.AllScreens.First();

    private void UpdateScreen()
    {
        bool firstScreen = screenBounds.IsEmpty;
        double oldLength = EdgePath.Length(screenBounds.Width - Width, screenBounds.Height - Height);
        double progress = oldLength > 0 ? flock.LeaderDistance / oldLength : 0;
        Screen screen = SelectedScreen();
        screenBounds = settings.AvoidTaskbar ? screen.WorkingArea : screen.Bounds;
        int size = settings.Size;
        ClientSize = new Size(size, size);
        double newLength = EdgePath.Length(screenBounds.Width - Width, screenBounds.Height - Height);
        double distance = firstScreen ? Math.Max(0, screenBounds.Width - Width) : progress * newLength;
        flock.Place(distance, settings.Size * 1.15);
        UpdateFlock();
    }

    private void TickDuck()
    {
        double now = clock.Elapsed.TotalSeconds;
        double elapsed = Math.Clamp(now - lastTick, 0, 0.1);
        lastTick = now;
        if (flock.Tick(elapsed, paused, settings.Speed))
        {
            Quack();
        }

        UpdateFlock();
    }

    private void UpdateFlock()
    {
        double width = screenBounds.Width - Width;
        double height = screenBounds.Height - Height;
        bool stepping = !paused && flock.LeaderIsMoving
            && (int)(flock.LeaderWalkingDistance / (settings.Size * 0.16)) % 2 == 1;
        UpdateDuck(EdgePath.AtCornerHop(flock.LeaderDistance, width, height, settings.Size, settings.Size),
            screenBounds, settings.Size,
            stepping, flock.IsQuacking, backwards: flock.IsGathering);

        int babySize = settings.Size * 2 / 3;
        foreach (var duckling in ducklings)
        {
            EdgePose pose = EdgePath.AtCornerHop(duckling.Motion.Distance, width, height, settings.Size, babySize);
            bool babyStep = !paused && duckling.Motion.IsMoving
                && (int)(duckling.Motion.WalkingDistance / (babySize * 0.18)) % 2 == 1;
            duckling.Window.UpdateDuck(pose, screenBounds, babySize, babyStep);
        }
    }

    private void Quack()
    {
        PlayQuack();
        flock.BeginGathering();
        UpdateFlock();
    }

    private void PlayQuack()
    {
        if (!settings.SoundEnabled || soundFailed)
        {
            return;
        }

        try
        {
            sound.Load();
            sound.Play();
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException
            or System.TimeoutException)
        {
            soundFailed = true;
            tray.ShowBalloonTip(5000, "The duck has lost its voice",
                $"Quack audio is unavailable. The duck will keep waddling.\n{exception.Message}",
                ToolTipIcon.Warning);
        }
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e) => QueueScreenUpdate();

    private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => QueueScreenUpdate();

    private void QueueScreenUpdate()
    {
        if (IsHandleCreated && !IsDisposed && !Disposing)
        {
            BeginInvoke((Action)(() =>
            {
                if (!IsDisposed && !Disposing)
                {
                    UpdateScreen();
                }
            }));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Stop();
            foreach (var duckling in ducklings)
            {
                duckling.Window.Dispose();
            }
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;
            tray.Visible = false;
            tray.Dispose();
            timer.Dispose();
            menu.Dispose();
            sound.Dispose();
            soundStream.Dispose();
            duckIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
