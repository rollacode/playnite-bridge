use tao::event::{Event, WindowEvent};
use tao::event_loop::{ControlFlow, EventLoopBuilder};
use tao::window::{Window, WindowBuilder, Icon};
use tray_icon::{TrayIconBuilder, menu::{Menu, MenuEvent, MenuItem, CheckMenuItem, PredefinedMenuItem}};
use crate::autostart;
use wry::WebView;
use wry::WebViewBuilder;

const WINDOW_TITLE: &str = "Playnite Sync";
const WINDOW_WIDTH: f64 = 900.0;
const WINDOW_HEIGHT: f64 = 620.0;

#[derive(Debug, Clone)]
enum UserEvent {
    ShowWindow,
    ToggleAutostart,
    Quit,
}

pub fn run(port: u16) -> Result<(), Box<dyn std::error::Error>> {
    let event_loop = EventLoopBuilder::<UserEvent>::with_user_event()
        .build();
    let proxy = event_loop.create_proxy();

    // Build tray menu
    let menu = Menu::new();
    let item_open = MenuItem::new("Open Dashboard", true, None);
    let item_autostart = CheckMenuItem::new("Start with Windows", true, autostart::is_enabled(), None);
    let item_quit = MenuItem::new("Quit", true, None);
    menu.append(&item_open)?;
    menu.append(&PredefinedMenuItem::separator())?;
    menu.append(&item_autostart)?;
    menu.append(&PredefinedMenuItem::separator())?;
    menu.append(&item_quit)?;

    let open_id = item_open.id().clone();
    let autostart_id = item_autostart.id().clone();
    let quit_id = item_quit.id().clone();

    let tray_icon = create_tray_icon();
    let _tray = TrayIconBuilder::new()
        .with_menu(Box::new(menu))
        .with_tooltip("Playnite Sync — Running")
        .with_icon(tray_icon)
        .build()?;

    // Forward menu events to the main event loop (CheckMenuItem isn't Send,
    // so we can't touch it from a worker thread).
    let proxy_menu = proxy.clone();
    MenuEvent::set_event_handler(Some(move |event: MenuEvent| {
        let user_event = if event.id == open_id {
            UserEvent::ShowWindow
        } else if event.id == autostart_id {
            UserEvent::ToggleAutostart
        } else if event.id == quit_id {
            UserEvent::Quit
        } else {
            return;
        };
        let _ = proxy_menu.send_event(user_event);
    }));

    tracing::info!("Tray icon active. Right-click to open menu.");

    // Hold window + webview as Option so we can drop them on close
    let mut dashboard_window: Option<Window> = None;
    let mut dashboard_webview: Option<WebView> = None;
    let url = format!("http://localhost:{port}");

    event_loop.run(move |event, event_loop, control_flow| {
        *control_flow = ControlFlow::Wait;

        match event {
            Event::UserEvent(UserEvent::ShowWindow) => {
                if dashboard_window.is_none() {
                    match create_window(event_loop, &url) {
                        Ok((window, webview)) => {
                            dashboard_window = Some(window);
                            dashboard_webview = Some(webview);
                            tracing::info!("Dashboard window opened");
                        }
                        Err(e) => tracing::error!("Failed to create window: {e}"),
                    }
                }
            }
            Event::UserEvent(UserEvent::ToggleAutostart) => {
                // CheckMenuItem already flipped its visual state by the time we get here.
                let now_enabled = item_autostart.is_checked();
                let result = if now_enabled { autostart::enable() } else { autostart::disable() };
                match result {
                    Ok(_) => tracing::info!("Autostart {}", if now_enabled { "enabled" } else { "disabled" }),
                    Err(e) => {
                        tracing::error!("Failed to update autostart: {e}");
                        item_autostart.set_checked(!now_enabled);
                    }
                }
            }
            Event::UserEvent(UserEvent::Quit) => {
                tracing::info!("Quit requested");
                *control_flow = ControlFlow::Exit;
            }
            Event::WindowEvent { event: WindowEvent::CloseRequested, .. } => {
                // Drop webview first (must be dropped before window)
                dashboard_webview.take();
                dashboard_window.take();
            }
            _ => {}
        }
    });
}

fn create_window(
    event_loop: &tao::event_loop::EventLoopWindowTarget<UserEvent>,
    url: &str,
) -> Result<(Window, WebView), Box<dyn std::error::Error>> {
    let window_icon = create_window_icon();

    let mut builder = WindowBuilder::new()
        .with_title(WINDOW_TITLE)
        .with_inner_size(tao::dpi::LogicalSize::new(WINDOW_WIDTH, WINDOW_HEIGHT))
        .with_min_inner_size(tao::dpi::LogicalSize::new(640.0, 400.0));

    if let Some(icon) = window_icon {
        builder = builder.with_window_icon(Some(icon));
    }

    let window = builder.build(event_loop)?;

    let webview = WebViewBuilder::new()
        .with_url(url)
        .build(&window)?;

    Ok((window, webview))
}

fn create_window_icon() -> Option<Icon> {
    let icon_bytes = include_bytes!("../../icon_tray.png");
    let img = image::load_from_memory(icon_bytes).ok()?;
    let rgba = img.to_rgba8();
    let (w, h) = rgba.dimensions();
    Icon::from_rgba(rgba.into_raw(), w, h).ok()
}

fn create_tray_icon() -> tray_icon::Icon {
    let icon_bytes = include_bytes!("../../icon_tray.png");
    if let Ok(img) = image::load_from_memory(icon_bytes) {
        let rgba = img.to_rgba8();
        let (w, h) = rgba.dimensions();
        return tray_icon::Icon::from_rgba(rgba.into_raw(), w, h)
            .expect("Failed to create tray icon");
    }

    // Fallback
    let size = 16u32;
    let mut rgba = vec![0u8; (size * size * 4) as usize];
    for i in 0..(size * size) as usize {
        rgba[i * 4] = 61; rgba[i * 4 + 1] = 107; rgba[i * 4 + 2] = 255; rgba[i * 4 + 3] = 255;
    }
    tray_icon::Icon::from_rgba(rgba, size, size).expect("Failed to create tray icon")
}
