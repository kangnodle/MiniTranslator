Unicode true
RequestExecutionLevel user
SetCompressor /SOLID lzma
AllowSkipFiles off

!include "MUI2.nsh"
!include "x64.nsh"

!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\MiniTranslator"
!searchreplace DOTNET_ARCH "${APP_RUNTIME}" "win-" ""
!define MUI_ICON "..\apps\MiniTranslator.Wpf\Assets\app.ico"
!define MUI_UNICON "${MUI_ICON}"

Name "MiniTranslator"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\MiniTranslator"
InstallDirRegKey HKCU "${UNINSTALL_KEY}" "InstallLocation"

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_REBOOTLATER_DEFAULT
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Korean"
!insertmacro MUI_LANGUAGE "English"

LangString RuntimeSetup ${LANG_KOREAN} ".NET 11 Desktop Runtime을 확인합니다. 없으면 다운로드하여 설치합니다. Windows 관리자 권한 요청을 승인해 주세요."
LangString RuntimeSetup ${LANG_ENGLISH} "Checking .NET 11 Desktop Runtime. If missing, it will be downloaded and installed. Approve the Windows administrator prompt if requested."
LangString RuntimeFailure ${LANG_KOREAN} ".NET 11 Desktop Runtime을 준비하지 못했습니다. 인터넷 연결과 관리자 권한 요청 승인 여부를 확인한 뒤 다시 설치하세요. 자세한 오류는 설치 내역에서 확인할 수 있습니다."
LangString RuntimeFailure ${LANG_ENGLISH} "Could not prepare .NET 11 Desktop Runtime. Check your internet connection and approve the administrator prompt, then retry. See the installation details for the error."
LangString RemoveUserData ${LANG_KOREAN} "저장된 설정과 Codex 로그인 정보도 함께 삭제할까요?"
LangString RemoveUserData ${LANG_ENGLISH} "Also remove saved settings and Codex sign-in credentials?"

Function .onInit
!if "${APP_RUNTIME}" == "win-arm64"
    ${IfNot} ${IsNativeARM64}
        MessageBox MB_OK|MB_ICONSTOP "This installer requires Windows on ARM64." /SD IDOK
        SetErrorLevel 1
        Quit
    ${EndIf}
!else
    ${IfNot} ${RunningX64}
        MessageBox MB_OK|MB_ICONSTOP "MiniTranslator requires 64-bit Windows." /SD IDOK
        SetErrorLevel 1
        Quit
    ${EndIf}
!endif
    SetShellVarContext current
    SetRegView 64
FunctionEnd

Function un.onInit
    SetShellVarContext current
    SetRegView 64
FunctionEnd

Section "MiniTranslator"
    InitPluginsDir
    SetOutPath "$PLUGINSDIR"
    File "install-desktop-runtime.ps1"
    DetailPrint "$(RuntimeSetup)"
    nsExec::ExecToLog '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\install-desktop-runtime.ps1" -Architecture "${DOTNET_ARCH}"'
    Pop $0
    ${If} $0 == 3010
        SetRebootFlag true
        SetErrorLevel 3010
    ${ElseIf} $0 != 0
        MessageBox MB_OK|MB_ICONSTOP "$(RuntimeFailure)" /SD IDOK
        SetErrorLevel 1
        Quit
    ${EndIf}
    SetOutPath "$INSTDIR"
    File "${APP_DIRECTORY}\MiniTranslator.exe"
    File /r /x MiniTranslator.exe "${APP_DIRECTORY}\*"
    SetOutPath "$INSTDIR"
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    CreateShortcut "$SMPROGRAMS\MiniTranslator.lnk" "$INSTDIR\MiniTranslator.exe" "--show"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "MiniTranslator"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "MiniTranslator"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\MiniTranslator.exe"
    WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
    WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
    WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
    ClearErrors
    Delete "$INSTDIR\MiniTranslator.exe"
    ${If} ${Errors}
        MessageBox MB_OK|MB_ICONSTOP "Close MiniTranslator from the tray menu, then retry uninstalling." /SD IDOK
        SetErrorLevel 1
        Abort
    ${EndIf}
    !include "${UNINSTALL_LIST}"
    Delete "$INSTDIR\Uninstall.exe"
    Delete "$SMPROGRAMS\MiniTranslator.lnk"
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "MiniTranslator"
    DeleteRegKey HKCU "${UNINSTALL_KEY}"
    RMDir "$INSTDIR"
    MessageBox MB_YESNO|MB_ICONQUESTION "$(RemoveUserData)" /SD IDNO IDNO skip_user_data
    RMDir /r "$LOCALAPPDATA\MiniTranslator"
    RMDir /r "$APPDATA\MiniTranslator"
    skip_user_data:
SectionEnd
