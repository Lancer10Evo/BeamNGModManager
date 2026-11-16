<h1 align="center">
  BeamNG.drive Mod Manager — Кросс-версионный портативный менеджер модификаций и инструментов обслуживания.
</h1>

<hr />

<p align="center">
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/actions">
    <img src="https://img.shields.io/github/actions/workflow/status/Lancer10Evo/BeamNGModManager/release.yml?branch=main&style=for-the-badge&logo=githubactions&logoColor=white&label=BUILD" alt="Build Status" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/releases/latest">
    <img src="https://img.shields.io/github/v/release/Lancer10Evo/BeamNGModManager?style=for-the-badge&logo=cakephp&logoColor=white&label=RELEASE&color=007ec6" alt="Latest Release" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/releases">
    <img src="https://img.shields.io/github/downloads/Lancer10Evo/BeamNGModManager/total?style=for-the-badge&logo=github&logoColor=white&label=DOWNLOADS&color=2cbe4e" alt="Total Downloads" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/commits/main">
    <img src="https://img.shields.io/github/commit-activity/w/Lancer10Evo/BeamNGModManager?style=for-the-badge&logo=git&logoColor=white&label=COMMIT%20ACTIVITY&color=007ec6" alt="Commit Activity" />
  </a>
  <a href="https://github.com/Lancer10Evo/BeamNGModManager/blob/main/LICENSE">
    <img src="https://img.shields.io/github/license/Lancer10Evo/BeamNGModManager?style=for-the-badge&label=LICENSE&color=ff6600" alt="License" />
  </a>
</p>

**BeamNG.drive Mod Manager** — это высокоуровневый десктопный инструмент для управления модификациями, профилями и кэшем игры **BeamNG.drive** (Windows 10/11 x64). Поставляется в формате одного портативного `.exe` файла и не требует установки сторонних библиотек или архиваторов.

---

## 📥 Скачать готовую программу
Перейдите в раздел **[Releases (Релизы)](https://github.com/Lancer10Evo/BeamNGModManager/releases/latest)** и скачайте **`BeamNGModManager.exe`**.

## ✨ Основные возможности и Аддоны
- **Автоопределение путей (Path Resolver):** автоматический поиск лицензионных версий (**Steam** через `libraryfolders.vdf` / `appmanifest_284160.acf` и **Epic Games** через `Manifests\*.item`), портативных сборок (`startup.ini`), новых папок `%LocalAppData%\BeamNG\BeamNG.drive\current\mods` и реестра Windows.
- **🛡️ Встроенный Симулятор Лицензий:** диагностический модуль для проверки совместимости со Steam и Epic Games.
- **📦 Менеджер Пресетов (Сборок):** сохранение активных наборов модификаций и переключение между сборками в 1 клик.
- **🏷️ Авто-Категоризатор модов:** автоматическое определение типа каждого архива (`🚗 Авто`, `🗺️ Карта`, `⚙️ Скрипт`, `🔊 Звук`) без его распаковки.
- **Умный установщик (Smart Installer):** поддержка Drag-and-Drop для `.zip`, `.rar`, `.7z`, распаковка «матрёшек», очистка рекламы и автоупаковка папок `vehicles/`, `levels/`, `lua/`, `art/` в валидный `.zip`.
- **Инструменты обслуживания:** безопасная очистка кэша `temp` (с сохранением шейдеров `shaders`), поиск файловых конфликтов и быстрый запуск игры.
- **12 языков интерфейса:** RU, EN, DE, ES, FR, PT, IT, PL, TR, ZH, JA, KO.

## 🛠 Сборка из исходников
<pre><code>dotnet publish -c Release -r win-x64 --self-contained true</code></pre>
