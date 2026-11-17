Unicode true
RequestExecutionLevel user
SetCompressor /SOLID lzma
AllowSkipFiles off

!include "MUI2.nsh"
!include "x64.nsh"

!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\MiniTranslator"
!define MUI_ICON "..\apps\MiniTranslator.Wpf\Assets\app.ico"
!define MUI_UNICON "${MUI_ICON}"

Name "MiniTranslator"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\MiniTranslator"
InstallDirRegKey HKCU "${UNINSTALL_KEY}" "InstallLocation"

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "Korean"
!insertmacro MUI_LANGUAGE "English"

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
SectionEnd
