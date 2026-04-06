// Windows autostart via HKCU\Software\Microsoft\Windows\CurrentVersion\Run
// No-op stubs on non-Windows platforms.

#[cfg(windows)]
const RUN_KEY: &str = r"Software\Microsoft\Windows\CurrentVersion\Run";
#[cfg(windows)]
const VALUE_NAME: &str = "PlayniteSync";

#[cfg(windows)]
pub fn is_enabled() -> bool {
    use winreg::enums::*;
    use winreg::RegKey;
    let hkcu = RegKey::predef(HKEY_CURRENT_USER);
    match hkcu.open_subkey(RUN_KEY) {
        Ok(key) => key.get_value::<String, _>(VALUE_NAME).is_ok(),
        Err(_) => false,
    }
}

#[cfg(windows)]
pub fn enable() -> std::io::Result<()> {
    use winreg::enums::*;
    use winreg::RegKey;
    let exe = std::env::current_exe()?;
    // Quote the path; no extra args — backend reads config from working dir,
    // but Run entries don't set CWD. Use --config with absolute path.
    let exe_dir = exe.parent().map(|p| p.to_path_buf()).unwrap_or_default();
    let config_path = exe_dir.join("config.toml");
    let cmd = format!("\"{}\" --config \"{}\"", exe.display(), config_path.display());

    let hkcu = RegKey::predef(HKEY_CURRENT_USER);
    let (key, _) = hkcu.create_subkey(RUN_KEY)?;
    key.set_value(VALUE_NAME, &cmd)?;
    Ok(())
}

#[cfg(windows)]
pub fn disable() -> std::io::Result<()> {
    use winreg::enums::*;
    use winreg::RegKey;
    let hkcu = RegKey::predef(HKEY_CURRENT_USER);
    let key = hkcu.open_subkey_with_flags(RUN_KEY, KEY_SET_VALUE)?;
    match key.delete_value(VALUE_NAME) {
        Ok(_) => Ok(()),
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => Ok(()),
        Err(e) => Err(e),
    }
}

#[cfg(not(windows))]
pub fn is_enabled() -> bool { false }
#[cfg(not(windows))]
pub fn enable() -> std::io::Result<()> { Ok(()) }
#[cfg(not(windows))]
pub fn disable() -> std::io::Result<()> { Ok(()) }
