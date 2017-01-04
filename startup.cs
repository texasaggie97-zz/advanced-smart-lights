Program.Setup(()=>{

    Program.AddFeature("Light,Dimmer,Siren,Switch", "Adv_SmartLights.Enable", "Enable Smart Light Control");
    Program.AddFeature("", "Light,Dimmer,Siren,Switch", "Adv_SmartLights.MotionDetector", "Controlled by motion sensor (enter name)", "module.text:any:sensor:status.level,sensor.motiondetect");
    Program.AddFeatureTextInput("Light,Dimmer", "Adv_SmartLights.Switch", "Controlled by Switch (enter name)");
    Program.AddFeatureTextInput("Light,Dimmer,Siren,Switch", "Adv_SmartLights.TurnoffTimeout", "Turn off after inactivity timeout (seconds)");
    Program.AddFeatureTextInput("Light,Dimmer,Switch", "Adv_SmartLights.MotionDelayStartWhenMotionStops", "Should the motion timer start when sensor sends an off command (TRUE), or when the initial on command is sent (FALSE)?");
    
    Program.AddInputField("Adv_SmartLight.EnableLog", "FALSE", "1) Enable log file");
    Program.AddInputField("Adv_SmartLight.LogPath", @"/usr/local/bin/homegenie/log/SmartLights.log", "2) Path to log file");
    Program.AddInputField("Adv_SmartLight.TimerFidelity", "1", "3) How often does code check for timer end (seconds)");
    Program.AddInputField("Adv_SmartLight.SwitchTimeout", "5", "4) How long between switch commands constitutes double tap (seconds)");
    Program.AddInputField("Adv_SmartLight.SensorNuisanceLimiter", "20", "5) Nuisance limiter - Two motion detects within limiter will trigger chime (seconds)");

});

return true;

