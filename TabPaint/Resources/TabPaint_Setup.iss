; -------------------------------------------------------------------------
; TabPaint 安装脚本 - 智能检测并下载 .NET 8 Desktop Runtime
; -------------------------------------------------------------------------

#define MyAppName "TabPaint"
#define MyAppVersion "0.9.1" ; 更新版本号
#define MyAppPublisher "TabPaint Team"
#define MyAppExeName "TabPaint.exe"
; 请确保此路径正确指向你的发布文件夹
#define MySourcePath "E:\dev\TabPaint_Release\*" 

[Setup]
AppId={{E638D92B-6886-4E90-9C08-0123456789AB} ; 建议生成一个新的GUID
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableDirPage=no
OutputBaseFilename=TabPaint_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
; SetupIconFile=YourIcon.ico

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MySourcePath}"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]

// -----------------------------------------------------------------------------
// 工具函数：版本号字符串比较
// 返回值: 
// > 0 : V1 > V2
// = 0 : V1 = V2
// < 0 : V1 < V2
// -----------------------------------------------------------------------------
function CompareVersion(V1, V2: string): Integer;
var
  P1, P2, N1, N2: Integer;
  S1, S2: string;
begin
  Result := 0;
  while (Result = 0) and ((V1 <> '') or (V2 <> '')) do
  begin
    P1 := Pos('.', V1);
    if P1 > 0 then
    begin
      S1 := Copy(V1, 1, P1 - 1);
      Delete(V1, 1, P1);
    end
    else
    begin
      S1 := V1;
      V1 := '';
    end;

    P2 := Pos('.', V2);
    if P2 > 0 then
    begin
      S2 := Copy(V2, 1, P2 - 1);
      Delete(V2, 1, P2);
    end
    else
    begin
      S2 := V2;
      V2 := '';
    end;

    N1 := StrToIntDef(S1, 0);
    N2 := StrToIntDef(S2, 0);
    Result := N1 - N2;
  end;
end;

// -----------------------------------------------------------------------------
// 核心逻辑：检测 .NET Desktop Runtime (x64) 是否满足版本要求
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
  
  // 临时文件用于存储命令输出
  TempFile := ExpandConstant('{tmp}\dotnet_runtimes.txt');
  CmdExe := ExpandConstant('{cmd}');

  // 运行 "dotnet --list-runtimes" 并将结果重定向到临时文件
  // SW_HIDE 确保没有任何黑框闪烁
  if Exec(CmdExe, '/C dotnet --list-runtimes > "' + TempFile + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    // 如果命令执行成功 (ResultCode = 0)，说明系统里有 dotnet 命令
    if ResultCode = 0 then
    begin
      if LoadStringsFromFile(TempFile, Lines) then
      begin
        for I := 0 to GetArrayLength(Lines) - 1 do
        begin
          // 在输出结果中查找是否有 "Microsoft.WindowsDesktop.App" 且版本以 "8." 开头
          // 典型输出如: Microsoft.WindowsDesktop.App 8.0.0 [C:\Program Files\dotnet\shared\...]
          if (Pos('Microsoft.WindowsDesktop.App 8.', Lines[I]) > 0) then
          begin
            Result := True;
            // Log('检测到 .NET 8 Desktop Runtime: ' + Lines[I]);
            Break;
          end;
        end;
      end;
    end;
  end;

  // 如果上面的执行失败（比如没有安装任何 dotnet，连命令都没有），Result 依然是 False，符合逻辑
  
  // 清理临时文件，忽视错误
  DeleteFile(TempFile);
end;

var
  DownloadPage: TDownloadWizardPage;
  NeedDotNet: Boolean;

// -----------------------------------------------------------------------------
// 初始化向导
// -----------------------------------------------------------------------------
procedure InitializeWizard;
begin
  // 预先检测是否需要安装，避免重复计算
  NeedDotNet := not IsDotNetDesktopRuntimeInstalled();
  
  // 创建下载页面
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

// -----------------------------------------------------------------------------
// 下一步按钮点击事件
// -----------------------------------------------------------------------------
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  // 只有在“准备安装”页面 (wpReady) 点击下一步时，且确实需要 .NET 时才触发
  if (CurPageID = wpReady) and NeedDotNet then
  begin
    DownloadPage.Clear;
    // 添加下载任务
    // 注意：尽量使用微软官方的“最新补丁”链接 (Rolling link)，或者锁定具体版本
    DownloadPage.Add('https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe', 'windowsdesktop-runtime.exe', '');
    
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
        Result := True;
      except
        // 下载失败处理
        if SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK) = IDOK then
          Result := False;
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

// -----------------------------------------------------------------------------
// 安装步骤变更事件
// -----------------------------------------------------------------------------
procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorCode: Integer;
  InstallerPath: string;
begin
  // 在 ssInstall (复制文件阶段开始前) 执行静默安装
  if (CurStep = ssInstall) and NeedDotNet then
  begin
    InstallerPath := ExpandConstant('{tmp}\windowsdesktop-runtime.exe');
    if FileExists(InstallerPath) then
    begin
      // 显示状态信息
      WizardForm.StatusLabel.Caption := '正在安装 .NET 8.0 Desktop Runtime，这可能需要一点时间...';
      
      // 执行安装：/install /quiet /norestart
      // Log('正在运行 .NET 安装程序...');
      if not ShellExec('', InstallerPath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ErrorCode) then
      begin
         MsgBox('.NET Runtime 安装失败。代码: ' + IntToStr(ErrorCode) + #13#10 + '请手动安装 .NET 8 Desktop Runtime。', mbError, MB_OK);
      end
      else
      begin
         // 安装成功后，标记以后不需要再安装（理论上不需要，因为变量生命周期仅限此次安装）
         // Log('.NET 安装完成。');
      end;
    end;
  end;
end;
