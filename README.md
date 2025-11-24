# WeatherHistory


A standalone Windows application that visualizes ERA5 historical weather data using animated cartoon weather scenes.
Originally built in JavaFX and later fully rewritten in **C# WPF** for smoother performance, a cleaner UI, and easier distribution.


---


## 🌦️ Features


### **Historical Weather Lookup**
- Pulls real historical weather data using the **Open-Meteo ERA5 archive API**
- Supports dates from **January 1, 1940** up to the present
- Detects future dates and invalid dates with friendly warnings


### **Animated Weather Engine**
Every weather type has its own cartoon animation, including:
- ☀️ **Sun**
- ☁️ **Clouds**
- 🌧️ **Rain**
- ❄️ **Snow**
- ⛈️ **Storms (lightning + flash effect)**


Animations are custom-drawn with WPF shapes, storyboards, and timers.


### **Development Tools**
Built-in Dev Tools allow forcing:
- Clear
- Cloudy
- Rain
- Snow
- Thunderstorm


Great for testing animation states without API calls.


### **UI Highlights**
- FoCo skyline background image
- Loading / error / unknown-weather screens
- Clean XAML layout
- Version History dialog with full chronological build timeline


---


## 🛠️ Tech Stack


- **C# (.NET 8)**
- **WPF / XAML**
- **Open-Meteo ERA5 API**
- **System.Text.Json**
- **Async/await HTTP fetching**


---


## 🔧 How to Build


1. **Clone the repository:**
```sh
git clone https://github.com/Intality/WeatherHistory.git
```


2. **Open the project in Visual Studio 2022 or later.**
- Make sure you have the **.NET 8 SDK** installed.
- All dependencies are included via the standard .NET libraries.


3. **Build the project:**
- Select `Release` or `Debug` mode.
- Press `Ctrl + Shift + B` or use **Build > Build Solution**.


4. **Run the app:**
- Hit `F5` to launch.


-Built with passion in Colorado — with a little lightning and a lot of love.
