using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.Run(new SetupForm());

sealed class SetupForm : Form
{
    const string ProductionServiceName = "TsdSupportAgent";
    const string LegacyLabServiceName = "TsdSupportAgentLab";
    const string AgentKeyName = "TSD-SUPPORT-AGENT-DEVICE-KEY-V1";
    const string DefaultCentral = "https://agent.toservicedesk.com.br";

    readonly TextBox _code = new() { Width = 250, CharacterCasing = CharacterCasing.Upper };
    readonly CheckBox _communication = new() {
        Text = "Ativar comunicação com a Central TSD (HTTPS)",
        AutoSize = true,
        Checked = true
    };
    readonly Button _install = new() { Text = "INSTALAR", Width = 120, Height = 34 };
    readonly Button _uninstall = new() { Text = "DESINSTALAR", Width = 120, Height = 30 };
    readonly Button _privacy = new() { Text = "PRIVACIDADE", Width = 120, Height = 30 };
    readonly Button _openLog = new() { Text = "ABRIR LOG", Width = 120, Height = 30 };
    readonly Label _status = new() { AutoSize = true, MaximumSize = new System.Drawing.Size(520, 0) };
    readonly string _logPath;
    readonly string _dataDir;
    readonly string _statePath;
    readonly string _configPath;
    readonly string _serviceName;
    readonly bool _existingIdentity;

    public SetupForm()
    {
        _dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TSD", "SupportAgent");
        _statePath = Path.Combine(_dataDir, "state.json");
        _configPath = Path.Combine(_dataDir, "config.json");
        _existingIdentity = File.Exists(_statePath);
        _serviceName = ResolveInstalledServiceName();

        var logDir = Path.Combine(_dataDir, "logs");
        Directory.CreateDirectory(logDir);
        _logPath = Path.Combine(logDir, "setup.log");
        SetupFileLog.Initialize(_logPath);
        SetupFileLog.Write($"SETUP_START user={WindowsIdentity.GetCurrent().Name}");

        var existingConfig = SetupConfiguration.Load(_configPath);
        _communication.Checked = existingConfig.communication_enabled;

        Text = "TSD Support Agent";
        Width = 620;
        Height = 395;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var title = new Label {
            Text = "TSD Support Agent",
            AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold)
        };
        var label = new Label {
            Text = _existingIdentity
                ? "Instalação existente detectada:"
                : "Código de instalação:",
            AutoSize = true
        };
        var central = new Label {
            Text = "Central: " + DefaultCentral,
            AutoSize = true
        };

        title.SetBounds(30, 22, 500, 35);
        label.SetBounds(30, 72, 280, 24);
        _code.SetBounds(30, 98, 250, 28);
        _install.SetBounds(310, 95, 120, 34);

        _communication.SetBounds(30, 145, 360, 25);
        central.SetBounds(50, 173, 500, 22);
        _privacy.SetBounds(30, 205, 120, 30);

        _status.SetBounds(30, 250, 520, 55);
        _openLog.SetBounds(310, 315, 120, 30);
        _uninstall.SetBounds(440, 315, 120, 30);

        Controls.AddRange([
            title, label, _code, _install, _communication,
            central, _privacy, _status, _openLog, _uninstall
        ]);

        if (_existingIdentity)
        {
            _code.Text = "Identidade será preservada";
            _code.Enabled = false;
            _install.Text = "ATUALIZAR";
            _status.Text = "O Agent já está cadastrado. Nenhum novo código é necessário.";
        }

        _uninstall.Visible = _existingIdentity
            || ServiceRegistryExists(ProductionServiceName)
            || ServiceRegistryExists(LegacyLabServiceName);

        _communication.CheckedChanged += (_, _) => UpdateCodeState();
        _install.Click += async (_, _) => await InstallAsync();
        _privacy.Click += (_, _) => ShowPrivacyPolicyDialog(false);
        _uninstall.Click += (_, _) => Uninstall();
        _openLog.Click += (_, _) => OpenLog();

        UpdateCodeState();
    }

    void UpdateCodeState()
    {
        if (_existingIdentity)
        {
            _code.Enabled = false;
            return;
        }

        _code.Enabled = _communication.Checked;
        if (!_communication.Checked)
            _code.Text = "";
    }

    static bool ServiceRegistryExists(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services\" + name);
        return key is not null;
    }

    static string ResolveInstalledServiceName()
    {
        if (ServiceRegistryExists(ProductionServiceName))
            return ProductionServiceName;
        if (ServiceRegistryExists(LegacyLabServiceName))
            return LegacyLabServiceName;
        return ProductionServiceName;
    }

    bool ShowPrivacyPolicyDialog(bool requireContinue)
    {
        using var dialog = new Form {
            Text = "TSD Support Agent — Privacidade",
            Width = 650,
            Height = 560,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var text = new TextBox {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = PrivacyPolicyText.Value,
            Font = new System.Drawing.Font("Segoe UI", 9),
            WordWrap = true
        };
        text.SetBounds(20, 20, 595, 440);

        var ok = new Button {
            Text = requireContinue ? "CONTINUAR" : "FECHAR",
            DialogResult = DialogResult.OK,
            Width = 120,
            Height = 32
        };
        ok.SetBounds(495, 475, 120, 32);

        dialog.Controls.Add(text);
        dialog.Controls.Add(ok);
        dialog.AcceptButton = ok;

        if (requireContinue)
        {
            var cancel = new Button {
                Text = "CANCELAR",
                DialogResult = DialogResult.Cancel,
                Width = 120,
                Height = 32
            };
            cancel.SetBounds(365, 475, 120, 32);
            dialog.Controls.Add(cancel);
            dialog.CancelButton = cancel;
        }

        return dialog.ShowDialog(this) == DialogResult.OK;
    }

    async Task InstallAsync()
    {
        if (!ShowPrivacyPolicyDialog(true))
        {
            SetupFileLog.Write($"PRIVACY_CANCELLED version={PrivacyPolicyText.Version}");
            return;
        }

        SetupFileLog.Write(
            $"PRIVACY_ACCEPTED version={PrivacyPolicyText.Version} communication={_communication.Checked}");

        _install.Enabled = false;
        string? rollbackPath = null;
        string? agentPath = null;
        var rollbackRestored = false;
        var requestedCommunicationEnabled = _communication.Checked;

        try
        {
            var programDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "TSD", "Support Agent");
            var dataDir = _dataDir;
            agentPath = Path.Combine(programDir, "TsdSupportAgent.exe");
            var codePath = Path.Combine(dataDir, "enrollment.code");
            var statePath = _statePath;
            var onlinePath = Path.Combine(dataDir, "online.ok");
            var configPath = _configPath;
            var logDir = Path.Combine(dataDir, "logs");
            var existingIdentity = File.Exists(statePath);
            var communicationEnabled = requestedCommunicationEnabled;

            var raw = "";
            if (!existingIdentity && communicationEnabled)
            {
                raw = Regex.Replace(
                    _code.Text ?? "",
                    "[^A-Z0-9]",
                    "",
                    RegexOptions.IgnoreCase).ToUpperInvariant();

                if (raw.Length != 12)
                    throw new InvalidOperationException("Informe o código de 12 caracteres.");
            }

            if (existingIdentity)
                SetupFileLog.Write($"UPDATE_BEGIN identity=PRESERVE communication={communicationEnabled}");
            else
                SetupFileLog.Write($"INSTALL_BEGIN communication={communicationEnabled} code={(communicationEnabled ? "REDACTED" : "NOT_REQUIRED")}");

            _status.Text = existingIdentity ? "Preparando atualização..." : "Preparando instalação...";

            SetupFileLog.Write($"PATHS program={programDir} data={dataDir}");
            Directory.CreateDirectory(programDir);
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(logDir);
            SecureDirectory(dataDir);
            SecureLogsDirectory(logDir);
            SetupFileLog.Write("DIRECTORIES_OK");

            SetupConfiguration.Save(configPath, communicationEnabled);
            SecureFile(configPath);
            SetupFileLog.Write($"CONFIG_WRITTEN communication={communicationEnabled} privacy={PrivacyPolicyText.Version}");

            StopInstalledServiceIfExists(_serviceName);
            SetupFileLog.Write($"SERVICE_STOPPED_OR_ABSENT name={_serviceName}");

            if (File.Exists(onlinePath)) File.Delete(onlinePath);

            rollbackPath = ReplaceAgentWithRollback(agentPath);
            SetupFileLog.Write($"AGENT_REPLACED rollback={(rollbackPath is null ? "none" : "present")}");

            if (!existingIdentity && communicationEnabled)
            {
                await File.WriteAllTextAsync(codePath, raw);
                SecureFile(codePath);
                SetupFileLog.Write("ENROLLMENT_CODE_WRITTEN_REDACTED");
            }
            else if (File.Exists(codePath))
            {
                File.Delete(codePath);
                SetupFileLog.Write("STALE_ENROLLMENT_CODE_REMOVED");
            }

            _status.Text = existingIdentity ? "Atualizando serviço..." : "Registrando serviço...";
            EnsureService(agentPath, _serviceName);
            SetupFileLog.Write($"SERVICE_ENSURED name={_serviceName}");
            LogServiceImagePath(_serviceName);
            StartInstalledService(_serviceName);
            SetupFileLog.Write("SERVICE_START_REQUESTED");

            if (communicationEnabled)
            {
                _status.Text = "Aguardando confirmação da Central...";
                for (var i = 0; i < 60 && !File.Exists(onlinePath); i++)
                    await Task.Delay(1000);

                if (!File.Exists(onlinePath))
                    throw new InvalidOperationException("O serviço iniciou, mas o sync com a Central não foi confirmado.");
            }
            else
            {
                _status.Text = "Validando serviço com comunicação desativada...";
                WaitForServiceRunning(_serviceName);
            }

            CommitAgentReplacement(rollbackPath);
            rollbackPath = null;

            SetupFileLog.Write(existingIdentity ? "UPDATE_SUCCESS" : "INSTALL_SUCCESS");
            _status.Text = communicationEnabled
                ? (existingIdentity
                    ? "✓ Atualização concluída. Identidade e cadastro preservados."
                    : "✓ Instalação concluída. Agent cadastrado e iniciado.")
                : "✓ Instalação concluída. Comunicação com a Central está desativada.";
            _install.Text = "CONCLUÍDO";
        }
        catch (Exception ex)
        {
            SetupFileLog.Write("INSTALL_OR_UPDATE_FAIL", ex);

            if (agentPath is not null && rollbackPath is not null)
            {
                try
                {
                    StopInstalledServiceIfExists(_serviceName);
                    RestoreAgentReplacement(agentPath, rollbackPath);
                    EnsureService(agentPath, _serviceName);

                    if (requestedCommunicationEnabled)
                    {
                        StartInstalledService(_serviceName);
                        SetupFileLog.Write("ROLLBACK_RESTORED");
                    }
                    else
                    {
                        DisableInstalledService(_serviceName);
                        SetupFileLog.Write("ROLLBACK_RESTORED_SERVICE_DISABLED_BY_PRIVACY");
                    }

                    rollbackRestored = true;
                }
                catch (Exception rollbackEx)
                {
                    SetupFileLog.Write("ROLLBACK_FAIL", rollbackEx);
                }
            }
            else if (_existingIdentity && agentPath is not null && File.Exists(agentPath))
            {
                try
                {
                    EnsureService(agentPath, _serviceName);

                    if (requestedCommunicationEnabled)
                    {
                        StartInstalledService(_serviceName);
                        SetupFileLog.Write("EXISTING_AGENT_RESTARTED_AFTER_FAILURE");
                    }
                    else
                    {
                        DisableInstalledService(_serviceName);
                        SetupFileLog.Write("EXISTING_AGENT_DISABLED_AFTER_FAILURE_BY_PRIVACY");
                    }

                    rollbackRestored = true;
                }
                catch (Exception restartEx)
                {
                    SetupFileLog.Write("EXISTING_AGENT_RESTART_FAIL", restartEx);
                }
            }

            _status.Text = "Falha: " + ex.Message
                + (rollbackRestored ? "\nA versão anterior foi restaurada." : "")
                + "\nConsulte o log em: " + _logPath;
            _install.Enabled = true;
        }
    }

    void Uninstall()
    {
        var answer = MessageBox.Show(
            "Desinstalar o TSD Support Agent?\n\n"
            + "O serviço, executável, dados locais e chave criptográfica serão removidos.\n"
            + "Registros já recebidos pela Central não são apagados automaticamente.",
            "TSD Support Agent",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
            return;

        _install.Enabled = false;
        _uninstall.Enabled = false;

        try
        {
            SetupFileLog.Write("UNINSTALL_BEGIN");

            foreach (var name in new[] { ProductionServiceName, LegacyLabServiceName })
            {
                try { StopInstalledServiceIfExists(name); } catch { }
                DeleteServiceByName(name);
            }

            DeleteAgentKey();

            var programDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "TSD", "Support Agent");

            if (Directory.Exists(programDir))
                Directory.Delete(programDir, true);

            SetupFileLog.Write("UNINSTALL_LOCAL_DATA_REMOVE");

            if (Directory.Exists(_dataDir))
                Directory.Delete(_dataDir, true);

            _status.Text = "✓ TSD Support Agent desinstalado. Feche esta janela para concluir.";
            _uninstall.Visible = false;
            _install.Enabled = false;
        }
        catch (Exception ex)
        {
            _status.Text = "Falha na desinstalação: " + ex.Message;
            _install.Enabled = true;
            _uninstall.Enabled = true;
        }
    }

    static void DeleteAgentKey()
    {
        var failures = new System.Collections.Generic.List<string>();

        foreach (var provider in new[] {
            CngProvider.MicrosoftPlatformCryptoProvider,
            CngProvider.MicrosoftSoftwareKeyStorageProvider
        })
        {
            var keyExisted = false;

            try
            {
                keyExisted = CngKey.Exists(
                    AgentKeyName,
                    provider,
                    CngKeyOpenOptions.MachineKey);

                if (!keyExisted)
                    continue;

                using (var key = CngKey.Open(
                    AgentKeyName,
                    provider,
                    CngKeyOpenOptions.MachineKey))
                {
                    key.Delete();
                }

                if (CngKey.Exists(AgentKeyName, provider, CngKeyOpenOptions.MachineKey))
                    failures.Add(provider.Provider + ": chave permaneceu após Delete");
            }
            catch (Exception ex)
            {
                if (keyExisted)
                    failures.Add(provider.Provider + ": " + ex.Message);
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                "Não foi possível remover completamente a chave do Agent: "
                + string.Join(" | ", failures));
    }

    static void DeleteServiceByName(string serviceName)
    {
        const uint SC_MANAGER_CONNECT = 0x0001;
        const uint DELETE = 0x00010000;

        var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var svc = OpenService(scm, serviceName, DELETE);
            if (svc == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                if (error == 1060) return;
                throw new System.ComponentModel.Win32Exception(error);
            }

            try
            {
                if (!DeleteService(svc))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 1072)
                        throw new System.ComponentModel.Win32Exception(error);
                }
            }
            finally { CloseServiceHandle(svc); }
        }
        finally { CloseServiceHandle(scm); }
    }

    static string? ReplaceAgentWithRollback(string target)
    {
        using var src = Assembly.GetExecutingAssembly().GetManifestResourceStream("TsdSupportAgent.exe")
            ?? throw new InvalidOperationException("Agent embutido não encontrado.");

        var tmp = target + ".new";
        var backup = target + ".rollback";
        if (File.Exists(tmp)) File.Delete(tmp);

        using (var dst = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            src.CopyTo(dst);

        string actualHash;
        using (var input = File.OpenRead(tmp))
            actualHash = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();

        if (!string.Equals(actualHash, EmbeddedAgentInfo.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tmp);
            throw new InvalidOperationException("Falha de integridade do Agent embutido.");
        }
        SetupFileLog.Write($"EMBEDDED_AGENT_HASH_OK version={EmbeddedAgentInfo.Version} sha256={actualHash}");

        string? rollback = null;
        try
        {
            if (File.Exists(backup)) File.Delete(backup);
            if (File.Exists(target))
            {
                File.Move(target, backup);
                rollback = backup;
            }

            File.Move(tmp, target);
            if (new FileInfo(target).Length < 1024 * 1024)
                throw new InvalidOperationException("Agent extraído possui tamanho inválido.");

            return rollback;
        }
        catch
        {
            if (File.Exists(tmp)) File.Delete(tmp);
            if (!File.Exists(target) && rollback is not null && File.Exists(rollback))
                File.Move(rollback, target);
            throw;
        }
    }

    static void CommitAgentReplacement(string? rollbackPath)
    {
        if (rollbackPath is not null && File.Exists(rollbackPath))
            File.Delete(rollbackPath);
    }

    static void RestoreAgentReplacement(string target, string rollbackPath)
    {
        if (!File.Exists(rollbackPath))
            throw new InvalidOperationException("Binário de rollback não encontrado.");

        if (File.Exists(target)) File.Delete(target);
        File.Move(rollbackPath, target);
    }


    void OpenLog()
    {
        try
        {
            if (!File.Exists(_logPath)) SetupFileLog.Write("LOG_OPEN_REQUEST");
            Process.Start(new ProcessStartInfo { FileName = _logPath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível abrir o log:\n" + ex.Message, "TSD Support Agent",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    static void LogServiceImagePath(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\" + serviceName);
            var image = key?.GetValue("ImagePath")?.ToString() ?? "(ausente)";
            var start = key?.GetValue("Start")?.ToString() ?? "(ausente)";
            SetupFileLog.Write($"SERVICE_CONFIG image={image} start={start}");
        }
        catch (Exception ex)
        {
            SetupFileLog.Write("SERVICE_CONFIG_READ_FAIL", ex);
        }
    }

    static void SecureDirectory(string path)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.Traverse, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }

    static void SecureLogsDirectory(string path)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        var inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }

    static void SecureFile(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }

    static void EnsureService(string agentPath, string serviceName)
    {
        const uint SC_MANAGER_CONNECT = 0x0001;
        const uint SC_MANAGER_CREATE_SERVICE = 0x0002;
        const uint SERVICE_ALL_ACCESS = 0xF01FF;
        const uint SERVICE_CHANGE_CONFIG = 0x0002;
        const uint SERVICE_START = 0x0010;
        const uint SERVICE_WIN32_OWN_PROCESS = 0x10;
        const uint SERVICE_AUTO_START = 0x2;
        const uint SERVICE_ERROR_NORMAL = 0x1;
        const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

        var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE);
        if (scm == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        var binaryPath = "\"" + agentPath + "\"";
        try
        {
            var svc = OpenService(scm, serviceName, SERVICE_CHANGE_CONFIG | SERVICE_START);
            if (svc != IntPtr.Zero)
            {
                SetupFileLog.Write("SERVICE_EXISTS repair=true");
                try
                {
                    if (!ChangeServiceConfig(
                        svc,
                        SERVICE_NO_CHANGE,
                        SERVICE_AUTO_START,
                        SERVICE_ERROR_NORMAL,
                        binaryPath,
                        null,
                        IntPtr.Zero,
                        null,
                        null,
                        null,
                        "TSD Support Agent"))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                finally { CloseServiceHandle(svc); }
                return;
            }

            svc = CreateService(
                scm,
                serviceName,
                "TSD Support Agent",
                SERVICE_ALL_ACCESS,
                SERVICE_WIN32_OWN_PROCESS,
                SERVICE_AUTO_START,
                SERVICE_ERROR_NORMAL,
                binaryPath,
                null, IntPtr.Zero, null, null, null);

            if (svc == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                var description = new SERVICE_DESCRIPTION { lpDescription = "TSD Support Agent — monitoramento e suporte autorizado." };
                var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<SERVICE_DESCRIPTION>());
                try
                {
                    Marshal.StructureToPtr(description, ptr, false);
                    ChangeServiceConfig2(svc, 1, ptr);
                }
                finally { Marshal.FreeHGlobal(ptr); }
            }
            finally { CloseServiceHandle(svc); }
        }
        finally { CloseServiceHandle(scm); }
    }

    static void StopInstalledServiceIfExists(string serviceName)
    {
        const uint SC_MANAGER_CONNECT = 0x0001;
        const uint SERVICE_STOP = 0x0020;
        const uint SERVICE_QUERY_STATUS = 0x0004;
        const uint SERVICE_CONTROL_STOP = 0x00000001;
        const uint SERVICE_STOPPED = 0x00000001;

        var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var svc = OpenService(
                scm,
                serviceName,
                SERVICE_STOP | SERVICE_QUERY_STATUS);

            if (svc == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                if (error == 1060) return;
                throw new System.ComponentModel.Win32Exception(error);
            }

            try
            {
                var status = new SERVICE_STATUS();
                if (!QueryServiceStatus(svc, ref status))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                if (status.dwCurrentState == SERVICE_STOPPED) return;

                if (!ControlService(svc, SERVICE_CONTROL_STOP, ref status))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 1062)
                        throw new System.ComponentModel.Win32Exception(error);
                }

                for (var i = 0; i < 120; i++)
                {
                    Thread.Sleep(250);
                    if (!QueryServiceStatus(svc, ref status))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    if (status.dwCurrentState == SERVICE_STOPPED) return;
                }

                throw new TimeoutException("O serviço não parou dentro de 30 segundos.");
            }
            finally
            {
                CloseServiceHandle(svc);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    static void DisableInstalledService(string serviceName)
    {
        const uint SC_MANAGER_CONNECT = 0x0001;
        const uint SERVICE_CHANGE_CONFIG = 0x0002;
        const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        const uint SERVICE_DISABLED = 0x00000004;

        var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var svc = OpenService(scm, serviceName, SERVICE_CHANGE_CONFIG);
            if (svc == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                if (!ChangeServiceConfig(
                    svc,
                    SERVICE_NO_CHANGE,
                    SERVICE_DISABLED,
                    SERVICE_NO_CHANGE,
                    null,
                    null,
                    IntPtr.Zero,
                    null,
                    null,
                    null,
                    null))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { CloseServiceHandle(svc); }
        }
        finally { CloseServiceHandle(scm); }
    }

    static void WaitForServiceRunning(string serviceName)
    {
        const uint SC_MANAGER_CONNECT = 0x0001;
        const uint SERVICE_QUERY_STATUS = 0x0004;
        const uint SERVICE_RUNNING = 0x00000004;

        var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var svc = OpenService(scm, serviceName, SERVICE_QUERY_STATUS);
            if (svc == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                var status = new SERVICE_STATUS();
                for (var i = 0; i < 80; i++)
                {
                    if (!QueryServiceStatus(svc, ref status))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

                    if (status.dwCurrentState == SERVICE_RUNNING)
                        return;

                    Thread.Sleep(250);
                }

                throw new TimeoutException("O serviço não entrou em execução dentro de 20 segundos.");
            }
            finally { CloseServiceHandle(svc); }
        }
        finally { CloseServiceHandle(scm); }
    }

    static void StartInstalledService(string serviceName)
    {
        const uint SC_MANAGER_CONNECT = 0x0001;
        const uint SERVICE_START = 0x0010;

        var scm = OpenSCManager(null, null, SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var svc = OpenService(scm, serviceName, SERVICE_START);
            if (svc == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                if (!StartService(svc, 0, null))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != 1056) throw new System.ComponentModel.Win32Exception(error);
                }
            }
            finally { CloseServiceHandle(svc); }
        }
        finally { CloseServiceHandle(scm); }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SERVICE_DESCRIPTION { public string lpDescription; }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint access);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateService(
        IntPtr scm, string serviceName, string displayName, uint desiredAccess,
        uint serviceType, uint startType, uint errorControl, string binaryPath,
        string? loadOrderGroup, IntPtr tagId, string? dependencies,
        string? serviceStartName, string? password);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr OpenService(IntPtr scm, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool StartService(IntPtr service, int argc, string[]? argv);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool ControlService(
        IntPtr service,
        uint control,
        ref SERVICE_STATUS serviceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool QueryServiceStatus(
        IntPtr service,
        ref SERVICE_STATUS serviceStatus);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool ChangeServiceConfig(
        IntPtr service,
        uint serviceType,
        uint startType,
        uint errorControl,
        string? binaryPathName,
        string? loadOrderGroup,
        IntPtr tagId,
        string? dependencies,
        string? serviceStartName,
        string? password,
        string? displayName);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool ChangeServiceConfig2(IntPtr service, uint infoLevel, IntPtr info);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool DeleteService(IntPtr service);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool CloseServiceHandle(IntPtr handle);
}

static class SetupFileLog
{
    static readonly object Gate = new();
    static string? PathValue;

    public static void Initialize(string path)
    {
        PathValue = path;
        Write("LOG_INIT");
    }

    public static void Write(string message, Exception? ex = null)
    {
        var path = PathValue;
        if (string.IsNullOrWhiteSpace(path)) return;

        lock (Gate)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(path)!;
                Directory.CreateDirectory(dir);

                if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
                {
                    var old = path + ".1";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(path, old);
                }

                using var sw = new StreamWriter(path, append: true, System.Text.Encoding.UTF8);
                sw.Write(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                sw.Write(" ");
                sw.Write(message);
                if (ex is not null)
                {
                    sw.Write(" | ");
                    sw.Write(ex.GetType().FullName);
                    sw.Write(" | HRESULT=0x");
                    sw.Write(ex.HResult.ToString("X8"));
                    sw.Write(" | ");
                    sw.Write(ex.Message.Replace("\r", " ").Replace("\n", " "));
                }
                sw.WriteLine();
            }
            catch { }
        }
    }
}