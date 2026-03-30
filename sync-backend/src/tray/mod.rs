use tao::event::{Event, WindowEvent};
use tao::event_loop::{ControlFlow, EventLoopBuilder};
use tao::window::{Window, WindowBuilder, Icon};
use tray_icon::{TrayIconBuilder, menu::{Menu, MenuEvent, MenuItem, PredefinedMenuItem}};
use wry::WebView;
use wry::WebViewBuilder;

const WINDOW_TITLE: &str = "Playnite Sync";
const WINDOW_WIDTH: f64 = 900.0;
const WINDOW_HEIGHT: f64 = 620.0;

#[derive(Debug, Clone, Copy, PartialEq)]
enum UserEvent {
    ShowWindow,
    Quit,
}

pub fn run(port: u16) -> Result<(), Box<dyn std::error::Error>> {
    let event_loop = EventLoopBuilder::<UserEvent>::with_user_event()
        .build();
    let proxy = event_loop.create_proxy();

    // Build tray menu
    let menu = Menu::new();
    let item_open = MenuItem::new("Open Dashboard", true, None);
    let item_quit = MenuItem::new("Quit", true, None);
    menu.append(&item_open)?;
    menu.append(&PredefinedMenuItem::separator())?;
    menu.append(&item_quit)?;

    let open_id = item_open.id().clone();
    let quit_id = item_quit.id().clone();

    let tray_icon = create_tray_icon();
    let _tray = TrayIconBuilder::new()
        .with_menu(Box::new(menu))
        .with_tooltip("Playnite Sync — Running")
        .with_icon(tray_icon)
        .build()?;

    let proxy_menu = proxy.clone();
    std::thread::spawn(move || {
        loop {
            if let Ok(event) = MenuEvent::receiver().recv() {
                if event.id() == &open_id {
                    let _ = proxy_menu.send_event(UserEvent::ShowWindow);
                } else if event.id() == &quit_id {
                    let _ = proxy_menu.send_event(UserEvent::Quit);
                }
            }
        }
    });

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
