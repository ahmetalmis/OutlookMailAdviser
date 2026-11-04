"""Outlook Mail Adviser — system tray start/stop controller (Windows)."""

from __future__ import annotations

import argparse
import os
import re
import shutil
import socket
import ssl
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from pathlib import Path

import pystray
from PIL import Image, ImageDraw
from pystray import MenuItem as item

ROOT = Path(__file__).resolve().parent.parent
LOCAL_CONFIG = ROOT / "start-dev.local.cmd"
LOG_DIR = ROOT / "tray" / "logs"
API_PROJECT = ROOT / "backend" / "src" / "OutlookMailAdviser.Api"
ADDIN_DIR = ROOT / "addin"

MUTEX_NAME = "Global\\OutlookMailAdviserTray_v1"
LOCK_FILE = ROOT / "tray" / ".tray.instance.lock"

_api_proc: subprocess.Popen | None = None
_addin_proc: subprocess.Popen | None = None
_mutex_handle: int | None = None
_lock = threading.Lock()
_status = "Durduruldu"
_shutting_down = False


def process_exists(pid: int) -> bool:
    if pid <= 0:
        return False
    if os.name == "nt":
        import ctypes

        synchronize = 0x00100000
        handle = ctypes.windll.kernel32.OpenProcess(synchronize, False, pid)  # type: ignore[attr-defined]
        if handle:
            ctypes.windll.kernel32.CloseHandle(handle)  # type: ignore[attr-defined]
            return True
        return False
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


def _read_lock_pid() -> int | None:
    if not LOCK_FILE.exists():
        return None
    try:
        return int(LOCK_FILE.read_text(encoding="utf-8").strip())
    except (ValueError, OSError):
        return None


def cleanup_stale_instance() -> None:
    """Remove lock file when the recorded tray process is gone."""
    pid = _read_lock_pid()
    if pid is not None and process_exists(pid):
        return
    if LOCK_FILE.exists():
        try:
            LOCK_FILE.unlink()
        except OSError:
            pass


def _acquire_windows_mutex() -> bool:
    global _mutex_handle
    import ctypes

    kernel32 = ctypes.windll.kernel32
    error_already_exists = 183

    handle = kernel32.CreateMutexW(None, True, MUTEX_NAME)
    if not handle:
        return False
    if kernel32.GetLastError() == error_already_exists:
        kernel32.CloseHandle(handle)
        return False

    _mutex_handle = handle
    LOCK_FILE.parent.mkdir(parents=True, exist_ok=True)
    LOCK_FILE.write_text(str(os.getpid()), encoding="utf-8")
    return True


def acquire_singleton() -> bool:
    """Only one tray instance — Windows mutex; process ölünce kilit otomatik serbest."""
    cleanup_stale_instance()
    if os.name == "nt":
        return _acquire_windows_mutex()

    pid = _read_lock_pid()
    if pid is not None and process_exists(pid):
        return False
    LOCK_FILE.parent.mkdir(parents=True, exist_ok=True)
    LOCK_FILE.write_text(str(os.getpid()), encoding="utf-8")
    return True


def release_singleton() -> None:
    global _mutex_handle
    if LOCK_FILE.exists():
        try:
            LOCK_FILE.unlink()
        except OSError:
            pass
    if os.name == "nt" and _mutex_handle:
        import ctypes

        kernel32 = ctypes.windll.kernel32
        kernel32.ReleaseMutex(_mutex_handle)
        kernel32.CloseHandle(_mutex_handle)
        _mutex_handle = None


def shutdown_all() -> None:
    global _shutting_down
    _shutting_down = True
    stop_services(force=True)
    release_singleton()


def load_local_env() -> dict[str, str]:
    env: dict[str, str] = {}
    if not LOCAL_CONFIG.exists():
        return env

    pattern = re.compile(r'^\s*set\s+"?([^=]+)=([^"]*)"?', re.IGNORECASE)
    for raw_line in LOCAL_CONFIG.read_text(encoding="utf-8", errors="ignore").splitlines():
        line = raw_line.split("rem", 1)[0].strip()
        if not line.lower().startswith("set "):
            continue
        match = pattern.match(line)
        if match:
            key, value = match.group(1).strip(), match.group(2).strip()
            if key:
                env[key] = value
    return env


def merged_env() -> dict[str, str]:
    env = os.environ.copy()
    env.update(load_local_env())
    if not env.get("Ai__Provider"):
        env["Ai__Provider"] = "OpenAI"
    return env


def port_in_use(port: int) -> bool:
    """Check localhost on both IPv4 and IPv6 — Vite/Kestrel may bind to either."""
    checks = (
        (socket.AF_INET, "127.0.0.1"),
        (socket.AF_INET6, "::1"),
    )
    for family, host in checks:
        try:
            with socket.socket(family, socket.SOCK_STREAM) as sock:
                sock.settimeout(0.5)
                if sock.connect_ex((host, port)) == 0:
                    return True
        except OSError:
            continue
    return False


def _ssl_context() -> ssl.SSLContext:
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    return ctx


def _url_reachable(url: str, timeout: float = 2.5) -> bool:
    request = urllib.request.Request(url, method="GET")
    context = _ssl_context() if url.startswith("https://") else None
    try:
        with urllib.request.urlopen(request, timeout=timeout, context=context) as response:
            return 200 <= response.status < 500
    except urllib.error.HTTPError as exc:
        return exc.code < 500
    except Exception:
        return False


def api_is_up() -> bool:
    return port_in_use(7047) or port_in_use(5047)


def addin_is_up() -> bool:
    return port_in_use(3000)


def services_running() -> bool:
    return api_is_up() and addin_is_up()


def api_process_alive() -> bool:
    with _lock:
        proc = _api_proc
    return proc is not None and proc.poll() is None


def startup_failure_status() -> str:
    log_path = LOG_DIR / "api.log"
    try:
        tail = log_path.read_text(encoding="utf-8", errors="replace")[-5000:]
    except OSError:
        tail = ""
    marker = tail.rfind("--- ")
    latest = tail[marker:] if marker >= 0 else tail
    if "Derleme başarısız" in latest or "error CS" in latest or "error CA" in latest:
        return "Hata: API derlenemedi"
    return "Hata: API acilmadi"


def wait_for_services(timeout_seconds: int = 90) -> bool:
    global _status
    deadline = time.time() + timeout_seconds
    while time.time() < deadline:
        if services_running():
            _status = "Calisiyor"
            return True
        if not api_process_alive():
            _status = startup_failure_status()
            return False
        time.sleep(2)
    if services_running():
        _status = "Calisiyor"
        return True
    _status = startup_failure_status()
    return False


def kill_port(port: int) -> None:
    if os.name != "nt":
        return
    result = subprocess.run(
        ["netstat", "-ano"],
        capture_output=True,
        text=True,
        check=False,
    )
    for line in result.stdout.splitlines():
        if f":{port}" not in line or "LISTENING" not in line:
            continue
        parts = line.split()
        if not parts:
            continue
        pid = parts[-1]
        if pid.isdigit():
            subprocess.run(
                ["taskkill", "/PID", pid, "/F", "/T"],
                capture_output=True,
                check=False,
            )


def find_executable(name: str, windows_cmd_variant: str | None = None) -> str:
    if os.name == "nt" and windows_cmd_variant:
        path = shutil.which(windows_cmd_variant)
        if path:
            return path
    path = shutil.which(name)
    if not path:
        raise FileNotFoundError(f"'{name}' bulunamadi. PATH veya kurulumu kontrol edin.")
    return path


def ensure_prerequisites(env: dict[str, str]) -> None:
    find_executable("dotnet")
    find_executable("npm", "npm.cmd")

    provider = env.get("Ai__Provider", "OpenAI")
    if provider.lower() == "openai" and not env.get("OPENAI_API_KEY"):
        raise RuntimeError(
            "OPENAI_API_KEY tanimli degil. start-dev.local.cmd dosyasini olusturun."
        )
    if provider.lower() == "ollama":
        find_executable("ollama")

    if not (ADDIN_DIR / "node_modules").exists():
        raise RuntimeError(
            "addin/node_modules yok. Once start-dev.cmd veya 'npm ci' calistirin."
        )
    if not (ADDIN_DIR / ".certs" / "localhost.pfx").exists():
        raise RuntimeError(
            "HTTPS sertifikasi yok. Once start-dev.cmd veya 'npm run certs' calistirin."
        )


def spawn_process(
    command: list[str],
    cwd: Path,
    env: dict[str, str],
    log_name: str,
) -> subprocess.Popen:
    LOG_DIR.mkdir(parents=True, exist_ok=True)
    log_path = LOG_DIR / log_name
    log_file = log_path.open("a", encoding="utf-8")
    log_file.write(f"\n--- {time.strftime('%Y-%m-%d %H:%M:%S')} ---\n")
    log_file.flush()

    creationflags = 0
    if os.name == "nt":
        creationflags = subprocess.CREATE_NO_WINDOW  # type: ignore[attr-defined]

    return subprocess.Popen(
        command,
        cwd=str(cwd),
        env=env,
        stdout=log_file,
        stderr=subprocess.STDOUT,
        creationflags=creationflags,
    )


def start_services() -> tuple[bool, str]:
    global _api_proc, _addin_proc, _status

    with _lock:
        if services_running():
            _status = "Calisiyor"
            return True, "Servisler zaten calisiyor."

        if _api_proc and _api_proc.poll() is None:
            _status = "Baslatiliyor..."
            return True, "Servisler baslatiliyor."

        if port_in_use(7047) or port_in_use(3000):
            return False, "7047 veya 3000 portu dolu. Once Durdur deneyin."

        try:
            env = merged_env()
            ensure_prerequisites(env)

            dotnet = find_executable("dotnet")
            npm = find_executable("npm", "npm.cmd")

            _api_proc = spawn_process(
                [
                    dotnet,
                    "run",
                    "--project",
                    str(API_PROJECT),
                    "--launch-profile",
                    "https",
                ],
                ROOT,
                env,
                "api.log",
            )

            time.sleep(4)

            _addin_proc = spawn_process(
                [npm, "run", "dev"],
                ADDIN_DIR,
                env,
                "addin.log",
            )

            _status = "Baslatiliyor..."
            return True, "Servisler baslatildi. Portlar aciliyor..."
        except Exception as exc:  # noqa: BLE001
            stop_services(force=True)
            _status = f"Hata: {exc}"
            return False, str(exc)


def stop_services(force: bool = False) -> tuple[bool, str]:
    global _api_proc, _addin_proc, _status

    with _lock:
        stopped = False

        for proc, name in ((_addin_proc, "addin"), (_api_proc, "api")):
            if proc and proc.poll() is None:
                proc.terminate()
                try:
                    proc.wait(timeout=8)
                except subprocess.TimeoutExpired:
                    proc.kill()
                stopped = True
            elif proc and proc.poll() is not None:
                stopped = True

        _addin_proc = None
        _api_proc = None

        if force or port_in_use(7047) or port_in_use(3000):
            kill_port(3000)
            kill_port(7047)
            stopped = True

        _status = "Durduruldu" if stopped else "Zaten durdurulmus"
        return stopped, _status


def refresh_status() -> str:
    global _status

    api_up = api_is_up()
    addin_up = addin_is_up()

    if api_up and addin_up:
        _status = "Calisiyor"
    elif api_up and not addin_up:
        _status = "Kismi — API calisiyor, addin yok"
    elif addin_up and not api_up:
        _status = "Kismi — addin calisiyor, API yok"
    elif api_process_alive():
        _status = "Baslatiliyor..."
    elif _status.startswith("Hata"):
        return _status
    else:
        _status = "Durduruldu"
    return _status


def can_start() -> bool:
    return _status.startswith("Durduruldu") or _status.startswith("Zaten") or _status.startswith("Hata")


def can_stop() -> bool:
    return not can_start() or _status.startswith("Kismi") or _status.startswith("Baslatiliyor") or _status.startswith("Calisiyor")


def create_icon(running: bool) -> Image.Image:
    size = 64
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    color = (34, 139, 34, 255) if running else (110, 110, 110, 255)
    draw.ellipse((4, 4, size - 4, size - 4), fill=color)
    draw.rectangle((18, 22, 46, 42), fill=(255, 255, 255, 255))
    draw.polygon([(18, 22), (32, 32), (18, 42)], fill=(220, 220, 220, 255))
    return image


class TrayController:
    def __init__(self, auto_start: bool) -> None:
        self.auto_start = auto_start
        self.icon: pystray.Icon | None = None

    def run(self) -> None:
        if not LOCAL_CONFIG.exists():
            self._warn_missing_config()

        menu = pystray.Menu(
            item(self._status_menu_text, None, enabled=False),
            item("Baslat", self.on_start, enabled=self._can_start_menu, default=True),
            item("Durdur", self.on_stop, enabled=self._can_stop_menu),
            item("Yeniden Baslat", self.on_restart, enabled=self._can_restart_menu),
            pystray.Menu.SEPARATOR,
            item("Test sayfasi ac", self.on_open_test),
            item("Log klasoru ac", self.on_open_logs),
            pystray.Menu.SEPARATOR,
            item("Tamamen kapat", self.on_exit),
        )

        self.icon = pystray.Icon(
            "OutlookMailAdviser",
            create_icon(False),
            "Outlook Mail Adviser",
            menu,
        )

        if self.auto_start:
            threading.Thread(target=self._auto_start_worker, daemon=True).start()

        threading.Thread(target=self._status_worker, daemon=True).start()
        self.icon.run()
        shutdown_all()

    def _warn_missing_config(self) -> None:
        example = ROOT / "start-dev.local.cmd.example"
        message = (
            "start-dev.local.cmd bulunamadi.\n\n"
            f"Ornek dosyayi kopyalayin:\n{example.name} -> start-dev.local.cmd"
        )
        if os.name == "nt":
            import ctypes

            ctypes.windll.user32.MessageBoxW(  # type: ignore[attr-defined]
                0,
                message,
                "Outlook Mail Adviser",
                0x30,
            )

    def _status_menu_text(self, _menu_item: pystray.MenuItem) -> str:
        return f"Durum: {_status}"

    def _can_start_menu(self, _menu_item: pystray.MenuItem) -> bool:
        return can_start()

    def _can_stop_menu(self, _menu_item: pystray.MenuItem) -> bool:
        return _status.startswith(("Calisiyor", "Baslatiliyor", "Kismi"))

    def _can_restart_menu(self, _menu_item: pystray.MenuItem) -> bool:
        return self._can_stop_menu(_menu_item) or _status.startswith("Hata")

    def _auto_start_worker(self) -> None:
        time.sleep(2)
        start_services()
        wait_for_services()
        self._update_icon()

    def _status_worker(self) -> None:
        while not _shutting_down:
            time.sleep(3)
            if _shutting_down or not self.icon:
                break
            refresh_status()
            self._update_icon()

    def _update_icon(self) -> None:
        if not self.icon:
            return
        status = refresh_status()
        title = f"Outlook Mail Adviser — {status}"
        changed = self.icon.title != title
        self.icon.icon = create_icon(status == "Calisiyor")
        self.icon.title = title
        if changed:
            update_menu = getattr(self.icon, "update_menu", None)
            if update_menu is not None:
                update_menu()

    def on_start(self, _icon: pystray.Icon, _item: pystray.MenuItem) -> None:
        def worker() -> None:
            start_services()
            wait_for_services()
            self._update_icon()

        threading.Thread(target=worker, daemon=True).start()
        self._update_icon()

    def on_stop(self, _icon: pystray.Icon, _item: pystray.MenuItem) -> None:
        def worker() -> None:
            stop_services(force=True)
            self._update_icon()

        threading.Thread(target=worker, daemon=True).start()

    def on_restart(self, _icon: pystray.Icon, _item: pystray.MenuItem) -> None:
        def worker() -> None:
            stop_services(force=True)
            time.sleep(2)
            start_services()
            wait_for_services()
            self._update_icon()

        threading.Thread(target=worker, daemon=True).start()
        self._update_icon()

    def on_open_test(self, _icon: pystray.Icon, _item: pystray.MenuItem) -> None:
        import webbrowser

        webbrowser.open("https://localhost:7047/test")

    def on_open_logs(self, _icon: pystray.Icon, _item: pystray.MenuItem) -> None:
        LOG_DIR.mkdir(parents=True, exist_ok=True)
        if os.name == "nt":
            os.startfile(str(LOG_DIR))  # noqa: S606

    def on_exit(self, icon: pystray.Icon, _item: pystray.MenuItem) -> None:
        global _shutting_down
        _shutting_down = True
        try:
            icon.visible = False
        except Exception:  # noqa: BLE001
            pass
        icon.stop()


def main() -> int:
    parser = argparse.ArgumentParser(description="Outlook Mail Adviser tray controller")
    parser.add_argument(
        "--autostart",
        action="store_true",
        help="Tray acildiginda API ve addin'i otomatik baslat",
    )
    parser.add_argument(
        "--cleanup-stale",
        action="store_true",
        help="Eski tepsi kilidini temizle ve cik",
    )
    args = parser.parse_args()

    if args.cleanup_stale:
        cleanup_stale_instance()
        release_singleton()
        return 0

    if not acquire_singleton():
        return 0

    try:
        TrayController(auto_start=args.autostart).run()
    except KeyboardInterrupt:
        shutdown_all()
    finally:
        shutdown_all()

    os._exit(0)


if __name__ == "__main__":
    sys.exit(main())
