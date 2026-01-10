; -------------------------------------------------------------------------
; TabPaint 安装脚本 - 支持 Lite (在线) 和 Full (离线) 双模式
; -------------------------------------------------------------------------

#define MyAppName "TabPaint"
#define MyAppPublisher "TabPaint Team"
#define MyAppExeName "TabPaint.exe"

; 这些变量通常由 PowerShell 传入，这里设置默认值以防直接编译
#ifndef MyAppVersion
  #define MyAppVersion "0.9.1"
#endif
#ifndef MySourcePath
  #define MySourcePath "E:\dev\TabPaint_Release\*"
#endif

; 判断是否是 Full 版 (由 PowerShell 传入 /DUseBundledRuntime)
#ifdef UseBundledRuntime
  #define IsFullVersion
  ; 运行库安装包的物理路径 (由 PowerShell 传入)
  #ifndef RuntimeInstallerPath
    #define RuntimeInstallerPath "E:\dev\TabPaint_Release\windowsdesktop-runtime-8.0-win-x64.exe"
  #endif
  #define RuntimeExeName "dotnet_installer.exe"
#endif

[Setup]
AppId={{E638D92B-6886-4E90-9C08-0123456789AB}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=no
; 输出文件名已由 PowerShell 控制，这里只是后备
OutputBaseFilename=TabPaint_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 程序主文件
Source: "{#MySourcePath}"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; [关键修改] 注意 Flags 增加了 dontcopy
#ifdef IsFullVersion
Source: "{#RuntimeInstallerPath}"; DestDir: "{tmp}"; DestName: "{#RuntimeExeName}"; Flags: deleteafterinstall dontcopy
#endif


[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; -------------------------------------------------------------------------
; 1. 注册应用程序，使其能出现在“打开方式”->“选择其他应用”列表中
; -------------------------------------------------------------------------
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".png"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".jpg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".jpeg"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".bmp"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".gif"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".webp"; ValueData: ""

Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

; -------------------------------------------------------------------------
; 2. 将 TabPaint 添加到常见图片格式的推荐“打开方式”列表中
; -------------------------------------------------------------------------
Root: HKA; Subkey: "Software\Classes\.png\OpenWithList\{#MyAppExeName}"; ValueType: none; ValueName: ""; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\.jpg\OpenWithList\{#MyAppExeName}"; ValueType: none; ValueName: ""; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\.jpeg\OpenWithList\{#MyAppExeName}"; ValueType: none; ValueName: ""; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\.bmp\OpenWithList\{#MyAppExeName}"; ValueType: none; ValueName: ""; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\.webp\OpenWithList\{#MyAppExeName}"; ValueType: none; ValueName: ""; Flags: uninsdeletekey

[UninstallDelete]
; 删除 AppData\Local\TabPaint 文件夹及其所有内容
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"

[Code]

// -----------------------------------------------------------------------------
// 1. 版本比较工具
// -----------------------------------------------------------------------------
function CompareVersion(V1, V2: string): Integer;
var
  P1, P2, N1, N2: Integer;
  S1, S2: string;
begin
  Result := 0;
  while (Result = 0) and ((V1 <> '') or (V2 <> '')) do
  begin
    P1 := Pos('.', V1); if P1 > 0 then begin S1 := Copy(V1, 1, P1 - 1); Delete(V1, 1, P1); end else begin S1 := V1; V1 := ''; end;
    P2 := Pos('.', V2); if P2 > 0 then begin S2 := Copy(V2, 1, P2 - 1); Delete(V2, 1, P2); end else begin S2 := V2; V2 := ''; end;
    N1 := StrToIntDef(S1, 0); N2 := StrToIntDef(S2, 0);
    Result := N1 - N2;
  end;
end;

// -----------------------------------------------------------------------------
// 2. 检测逻辑
// -----------------------------------------------------------------------------
function IsDotNetDesktopRuntimeInstalled(): Boolean;
var
  TempFile: string;
  ResultCode: Integer;
  Lines: TArrayOfString;
  I: Integer;
  CmdExe: string;
begin
  Result := False;
  TempFile := ExpandConstant('{tmp}\dotnet_runtimes.txt');
  CmdExe := ExpandConstant('{cmd}');

  if Exec(CmdExe, '/C dotnet --list-runtimes > "' + TempFile + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
    begin
      if LoadStringsFromFile(TempFile, Lines) then
      begin
        for I := 0 to GetArrayLength(Lines) - 1 do
        begin
          // 检测 8.x 版本的 Desktop App
          if (Pos('Microsoft.WindowsDesktop.App 8.', Lines[I]) > 0) then
          begin
            Result := True;
            Break;
          end;
        end;
      end;
    end;
  end;
  DeleteFile(TempFile);
end;

var
  DownloadPage: TDownloadWizardPage;
  NeedDotNet: Boolean;

// -----------------------------------------------------------------------------
// 3. 初始化
// -----------------------------------------------------------------------------
procedure InitializeWizard;
begin
  NeedDotNet := not IsDotNetDesktopRuntimeInstalled();
  
  // 只有在 Lite 版本且需要安装时，才创建下载页面
  #ifndef IsFullVersion
  if NeedDotNet then
    DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
  #endif
end;

// -----------------------------------------------------------------------------
// 4. 下一步点击 (仅 Lite 版涉及下载)
// -----------------------------------------------------------------------------
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  
  #ifndef IsFullVersion
    if (CurPageID = wpReady) and NeedDotNet then
    begin
      DownloadPage.Clear;
      // 这里的链接可以用你自己的 OSS 链接替换，解决下载慢的问题
      DownloadPage.Add('https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe', 'windowsdesktop-runtime.exe', '');
      DownloadPage.Show;
      try
        try
          DownloadPage.Download;
          Result := True;
        except
          if SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK) = IDOK then
            Result := False;
        end;
      finally
        DownloadPage.Hide;
      end;
    end;
  #endif
end;

// -----------------------------------------------------------------------------
// 5. 安装步骤 (Lite版运行下载的文件，Full版运行解压的文件)
// -----------------------------------------------------------------------------
procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorCode: Integer;
  InstallerPath: string;
begin
  // 只在“开始安装”步骤，且检测到需要 .NET 时执行
  if (CurStep = ssInstall) and NeedDotNet then
  begin
    WizardForm.StatusLabel.Caption := '正在安装 .NET 8.0 Desktop Runtime，请稍候...';
    
    InstallerPath := '';

    #ifdef IsFullVersion
      // ============================================================
      // Full 版逻辑：手动从安装包内解压文件
      // ============================================================
      try
        // 关键修复：显式将文件解压到临时目录
        ExtractTemporaryFile('{#RuntimeExeName}');
        InstallerPath := ExpandConstant('{tmp}\{#RuntimeExeName}');
      except
        MsgBox('无法解压 .NET 运行库安装包。安装将继续，但程序可能无法运行。', mbError, MB_OK);
      end;
      
    #else
      // ============================================================
      // Lite 版逻辑：使用之前下载好的文件
      // ============================================================
      InstallerPath := ExpandConstant('{tmp}\windowsdesktop-runtime.exe');
    #endif

    // 执行安装
    if (InstallerPath <> '') and FileExists(InstallerPath) then
    begin
      // Log('正在运行运行库安装程序: ' + InstallerPath);
      if not ShellExec('', InstallerPath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ErrorCode) then
      begin
         // 1602 = 用户取消, 3010 = 需要重启 (视为成功)
         if (ErrorCode <> 1602) and (ErrorCode <> 3010) then
         begin
             MsgBox('.NET Runtime 安装失败。错误代码: ' + IntToStr(ErrorCode), mbError, MB_OK);
         end;
      end;
    end
    else
    begin
      // 如果走到这里，说明 Lite版下载失败 或 Full版解压失败
      MsgBox('找不到 .NET 运行库安装文件。', mbError, MB_OK);
    end;
  end;
end;
