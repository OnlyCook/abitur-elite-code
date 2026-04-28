<h1 align="center">
  <img src="assets/icons/app_icon.ico" alt="HKS Vertreter Icon" width="96" height="96">
  <br>
  Abitur Elite Code
</h1>

<p align="center">
  <a href="https://github.com/OnlyCook/abitur-elite-code/wiki">
    <img src="https://img.shields.io/badge/Wiki-Hilfestellungen-2ea44f?logo=github" alt="Wiki Button">
  </a>
</p>

<p align="center"> 
  Eine Open-Source-Desktop-Anwendung zur gezielten Vorbereitung auf die Programmier- und Datenbankanteile (OOP & SQL) des Praktische-Informatik-Abiturs in Hessen.
</p>

<p align="center" id="download">
  <a href="https://github.com/OnlyCook/abitur-elite-code/releases/latest/download/AbiturEliteCode-win.zip">
    <img src="https://img.shields.io/badge/Download-Windows-blue?style=for-the-badge"
    alt="Download für Windows">
  </a>
  <a href="https://github.com/OnlyCook/abitur-elite-code/releases/latest/download/AbiturEliteCode-linux.zip">
    <img src="https://img.shields.io/badge/Download-Linux-cc8400?style=for-the-badge&logo=linux" alt="Download für Linux">
  </a>
  <a href="https://github.com/OnlyCook/abitur-elite-code/releases/latest/download/AbiturEliteCode-mac.zip">
    <img src="https://img.shields.io/badge/Download-macOS-black?style=for-the-badge&logo=apple"
    alt="Download für macOS">
  </a>
</p>

## Über das Projekt
Abitur Elite Code bietet eine strukturierte Lernumgebung nach dem Vorbild von LeetCode, jedoch speziell auf die Anforderungen des hessischen Informatik-Abiturs ausgerichtet. Die Anwendung ermöglicht es Schülerinnen und Schülern, sowohl objektorientierte Programmierung (C#) als auch SQL (MySQL) praxisnah zu trainieren und ihre Kompetenzen systematisch auszubauen.

## Installation & Start

<h3>
  <img src="docs/github-images/ic_windows.svg" alt="Windows Icon" width="24" height="24">
  Windows
</h3>

1. `AbiturEliteCode-win.zip` entpacken
2. Den Ordner `AbiturEliteCode` öffnen
3. `AbiturEliteCode.exe` doppelklicken und fertig!

> **SmartScreen-Warnung?** „Weitere Informationen" → „Trotzdem ausführen" klicken.  
> Die Warnung erscheint, weil die App kein kostenpflichtiges Code-Signing-Zertifikat besitzt.  

---

<h3>
  <img src="docs/github-images/ic_linux.svg" alt="Windows Icon" width="24" height="24">
  Linux
</h3>

1. `AbiturEliteCode-linux.zip` entpacken
2. Ein Terminal im entpackten Ordner öffnen und ausführen:
```bash
   chmod +x AbiturEliteCode
   ./AbiturEliteCode
```

---

<h3>
  <img src="docs/github-images/ic_macos.svg" alt="Windows Icon" width="24" height="24">
  macOS
</h3>

1. `AbiturEliteCode-mac.zip` entpacken
2. Ein Terminal im entpackten Ordner öffnen und ausführen:
```bash
   chmod +x AbiturEliteCode
   ./AbiturEliteCode
```

> **Tipp:** Um ein Terminal im richtigen Ordner zu öffnen, den Ordner im Finder öffnen, dann **Rechtsklick auf einen leeren Bereich** → „Neues Terminal im Ordner" (macOS Sequoia) oder per **Finder → Dienste → Neues Terminal im Ordner**. 

> **Sicherheitswarnung beim ersten Start?**  
> Da die App nicht aus dem App Store stammt, blockiert macOS sie zunächst.  
> **Lösung:** Rechtsklick auf `AbiturEliteCode.app` → **„Öffnen"** → im erscheinenden Dialog erneut **„Öffnen"** klicken.  
> Dieser Schritt ist nur **einmalig** beim allerersten Start nötig.  
> **Alternative:** Falls dies nicht funktioniert, kann dieses Video weiterhelfen: https://youtu.be/zZEBE4b_xiQ

## Update-Anleitung

Abitur Elite Code bietet eine integrierte Update-Funktion, die dich visuell benachrichtigt (roter Punkt am Zahnrad), sobald eine neue Version verfügbar ist (zu finden in den Einstellungen unter `Updates`).

<h3>
  <img src="docs/github-images/ic_windows.svg" alt="Windows Icon" width="24" height="24">
  Windows (Auto-Update)
</h3>

Windows-Nutzer können die App bequem per Knopfdruck in den Einstellungen automatisch aktualisieren lassen. 

**Wichtig:** Damit das Auto-Update reibungslos funktioniert, muss sich die App an einem Ort befinden, für den **keine Administratorrechte** benötigt werden (z. B. auf dem Desktop, in den Dokumenten oder einem eigenen Ordner). Liegt die App in geschützten System-Verzeichnissen wie `C:\Program Files`, wird das automatische Update aus Sicherheitsgründen blockiert.

**Manuelles Update (Fallback):**
Sollte das Auto-Update fehlschlagen (z.B. wegen fehlender Berechtigungen), öffnet sich stattdessen ein Pop-Up-Dialog welcher durch deinen Browser die neue `.zip`-Datei herunterladen kann. In diesem Fall musst du das Update manuell durchführen. Entpacke dazu einfach die neue Version und ersetze die alten Dateien durch die neuen. 

> Hier ist ein kurzes Video, das den manuellen Update-Prozess auf Windows zeigt:
> https://github.com/user-attachments/assets/9e1c55fc-e0a7-4467-b5c8-9cabe17c4d52

---

<h3>
  <img src="docs/github-images/ic_linux.svg" alt="Linux Icon" width="24" height="24">
  <img src="docs/github-images/ic_macos.svg" alt="macOS Icon" width="24" height="24">
  Linux & macOS
</h3>

Auf Linux und macOS ist die Update-Benachrichtigung ebenfalls aktiv. Wenn du auf "App aktualisieren" klickst, öffnet sich ein Pop-Up-Dialog welcher die neueste Version durch einen Browser herunterladen kann.  
> Aufgrund von Betriebssystem-Einschränkungen gibt es hier **keinen** direkten Auto-Installer.

1. Lade die neue Version herunter und entpacke sie.
2. Du kannst die alte App/den alten Ordner löschen und stattdessen die neue Version an einen beliebigen Ort verschieben.

> **Keine Sorge um deine Speicherstände!** Auf Linux und macOS ist der "Portable Mode" standardmäßig deaktiviert. Das bedeutet, deine Fortschritte werden sicher in einem versteckten System-Verzeichnis abgelegt. Wenn du die neue Version der App öffnest (egal von wo), wird dein kompletter Fortschritt automatisch geladen (falls du den "Portable Mode" nicht aktiviert hast).

## Skip-Codes & Lösungen
Eine Übersicht aller Level-Codes sowie der Lösungen zu den jeweiligen Levels findest du in der [LEVEL_CODES.md](py/LEVEL_CODES.md).

## Level Designer

Mit dem integrierten Level Designer kannst du eigene Levels erstellen, die andere Nutzer lösen können.

**C# Levels**  
Die vollständige Dokumentation dazu findest du im [Wiki](https://github.com/OnlyCook/abitur-elite-code/wiki/CS_LEVEL_DESIGNER_GUIDE).
Wenn du Levels eigenständig mit KI generieren möchtest, hilft dir die [Anleitung zur KI-gestützten Level-Erstellung](https://github.com/OnlyCook/abitur-elite-code/wiki/CS_AI_LEVEL_CREATION_GUIDE) weiter.

**SQL Levels**  
Die vollständige Dokumentation dazu findest du im [Wiki](https://github.com/OnlyCook/abitur-elite-code/wiki/SQL_LEVEL_DESIGNER_GUIDE).
Wenn du Levels eigenständig mit KI generieren möchtest, hilft dir die [Anleitung zur KI-gestützten Level-Erstellung](https://github.com/OnlyCook/abitur-elite-code/wiki/SQL_AI_LEVEL_CREATION_GUIDE) weiter.
