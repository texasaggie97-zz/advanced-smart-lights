string ActivityLogPath = Program.InputField("Adv_SmartLight.LogPath").Value;
var ENABLE_LOG = Program.InputField("Adv_SmartLight.EnableLog").Value;
var SMART_LIGHT_ENABLE = "Adv_SmartLights.Enable";
var MOT_SENSOR_NAME = "Adv_SmartLights.MotionDetector";
var SWITCH_NAME = "Adv_SmartLights.Switch";
var TURNOFF_TIMEOUT = "Adv_SmartLights.TurnoffTimeout";
var MOT_DELAY_AFTER_OFF = "Adv_SmartLights.MotionDelayStartWhenMotionStops";
var TIMER_ON = "Adv_SmartLights.TimerOn";
var TIMER_FIDELITY = Program.InputField("Adv_SmartLight.TimerFidelity").DecimalValue;
var SWITCH_TIMEOUT = Program.InputField("Adv_SmartLight.SwitchTimeout").DecimalValue;
var NUISANCE_TIMEOUT = Program.InputField("Adv_SmartLight.SensorNuisanceLimiter").DecimalValue;
var time_sunrise = DateTime.Now;
var time_sunset = DateTime.Now;
bool Night = false;


Action<string> 
Log = (string logtext) => {
  if ((string.IsNullOrEmpty(ActivityLogPath) == false) && (ENABLE_LOG == "TRUE"))
  {
    string text = DateTime.Now.ToLocalTime().ToString("yyyyMMdd HH:mm:ss.fffffff") + " ; " + logtext + "\n";
    System.IO.File.AppendAllText(ActivityLogPath, text);
  }
};


// Process all module changes
When.ModuleParameterIsChanging((module, parameter) => 
{
  // Set sunrise/sunset times
  try
  {
    //jkUtils
    time_sunrise = DateTime.ParseExact(Program.WithName("jkUtils - Solar Altitude").Parameter("jkUtils.SolarAltitude.Morning.Sunrise.Start").Value, "H:mm", System.Globalization.CultureInfo.InvariantCulture);
    time_sunset = DateTime.ParseExact(Program.WithName("jkUtils - Solar Altitude").Parameter("jkUtils.SolarAltitude.Evening.Sunset.End").Value, "H:mm", System.Globalization.CultureInfo.InvariantCulture);

    //Log("jkUtils evaluated:\ntime_sunrise = " + time_sunrise + "\ntime_sunset = " + time_sunset + "\n\n");
  
  }
  catch (Exception e) 
  {
    Log("1-ERROR: could not generate sunrise/sunset times.\n" + e.Message + "\ntime_sunrise = " + time_sunrise + "\ntime_sunset = " + time_sunset + "\n\n");
    time_sunrise = DateTime.Now;
    time_sunset = DateTime.Now;
  }

  if (DateTime.Compare(DateTime.Now, time_sunrise)<0 || DateTime.Compare(DateTime.Now, time_sunset)>0)
  { 
    Night = true;
    //Log("Night");
  }
  else
  {
    Night = false;
    //Log("Day");
  }
  
  if (module.IsOfDeviceType("Switch"))
  {
    Modules.WithFeature(SMART_LIGHT_ENABLE).WithParameter(SWITCH_NAME).Each((switch_mod)=>
    {
      if (switch_mod.Parameter(SWITCH_NAME).Value == module.Instance.Name)
      { 
        Log("2-" + module.Instance.Name + " ; " + parameter.Name + " ; " + parameter.Value);
        
        if (module.Parameter("Status.Level").DecimalValue==1)
        {
          switch_mod.On();
          Log("3-" + switch_mod.Instance.Name + " ; ON");
        }
        else if (switch_mod.Parameter(TIMER_ON).Value == "FALSE")
        {
          //TIMER_ON should be TRUE if motion was sensed
          switch_mod.Off();
          Log("3.1-" + switch_mod.Instance.Name + " ; OFF (TIMER_ON false)");
        }
        else
        {
          //switch_mod is ON ; TIMER_ON is TRUE
          Log("3.2-" + switch_mod.Instance.Name + " ; is on, timer on, single switch OFF will not turn off light");
        }
        
        // Double switch detect
        if (module.Parameter("Status.Level").Statistics.History[0].Value == 1 && module.Parameter("Status.Level").Statistics.History[1].Value == 1)
        {          
          Log("9-" + module.Instance.Name + " ; Consecutine ON detected");

          var event1 = module.Parameter("Status.Level").Statistics.History[0].Timestamp;
          var event2 = module.Parameter("Status.Level").Statistics.History[1].Timestamp;
          var elapsed = new TimeSpan(event1.Ticks - event2.Ticks);
          Log("9.1-event1="+event1.ToLocalTime().ToString("yyyyMMdd HH:mm:ss.fffffff")+" ; event2="+event2.ToLocalTime().ToString("yyyyMMdd HH:mm:ss.fffffff")
              +" ; elapsed="+elapsed.TotalSeconds.ToString());
          if (elapsed.TotalSeconds < SWITCH_TIMEOUT)
          {
            // Double switch ON
            // Action:  turn light on and set timer
            Log("10-Double switch ON");

            switch_mod.Parameter(TIMER_ON).Value="TRUE";
            // UpdateTime should be automatically updated, but it doesn't seem to be.  This manually forces an update.
            switch_mod.Parameter(TIMER_ON).UpdateTime=DateTime.Now;

            switch_mod.On();
            Log("11-" + switch_mod.Instance.Name + " ; ON" + " ; Delay time start ; " + switch_mod.Parameter(TURNOFF_TIMEOUT).Value);           
          }
        }
        else if (module.Parameter("Status.Level").Statistics.History[0].Value == 0 && module.Parameter("Status.Level").Statistics.History[1].Value == 0 )
        {
          Log("12-" + module.Instance.Name + " ; Consecutine OFF detected");

          var event1 = module.Parameter("Status.Level").Statistics.History[0].Timestamp;
          var event2 = module.Parameter("Status.Level").Statistics.History[1].Timestamp;
          var elapsed = new TimeSpan(event1.Ticks - event2.Ticks);
          Log("12.1-event1="+event1.ToLocalTime().ToString("yyyyMMdd HH:mm:ss.fffffff")+" ; event2="+event2.ToLocalTime().ToString("yyyyMMdd HH:mm:ss.fffffff")
              +" ; elapsed="+elapsed.TotalSeconds.ToString());
          if (elapsed.TotalSeconds < SWITCH_TIMEOUT)
          {
            // Double switch OFF
            // Action: turn off lights immediately and cancel motion delay
            Log("13-Double switch OFF");

            switch_mod.Parameter(TIMER_ON).Value="FALSE";
            // UpdateTime should be automatically updated, but it doesn't seem to be.  This manually forces an update.
            switch_mod.Parameter(TIMER_ON).UpdateTime=DateTime.Now;

            switch_mod.Off();
            Log("14-" + switch_mod.Instance.Name + " ; OFF");
          }
          else
          {
            Log("14.1-Elapsed time ("+elapsed.TotalSeconds.ToString()+") > timeout("+SWITCH_TIMEOUT.ToString()+")");
          }
        }
        else
        {
          Log("14.2-" + module.Instance.Name + " ; " + module.Parameter("Status.Level").Statistics.History[0].Value + " " + module.Parameter("Status.Level").Statistics.History[1].Value + " ; No consecutive detected");
        }
      }
      
      return false;
    });
  }

  if (module.IsOfDeviceType("Sensor"))
  {
    if (parameter.Name == "Status.Level")
    {
      // Check history list to see if wind has caused xx triggers within xx minutes.  
      // If so, turn off chime for xx minutes.
      
      var motionlevel = parameter.DecimalValue;
      if (motionlevel > 0)
      {  // Motion sensed 
        Log("14.5-" + module.Instance.Name + " ; " + parameter.Name + " ; " + parameter.Value);
        
        Modules.WithFeature(SMART_LIGHT_ENABLE).Each((sensor_mod)=>
        {
          string fullName = module.Instance.Domain + ":" + module.Instance.Address;
          Log("14.6-" + module.Instance.Name + " ; " + sensor_mod.Parameter(MOT_SENSOR_NAME).Value + " ; " + fullName);

          if (module.Instance.Name.StartsWith(sensor_mod.Parameter(MOT_SENSOR_NAME).Value) || fullName.StartsWith(sensor_mod.Parameter(MOT_SENSOR_NAME).Value))
          { 
            // sensor_mod is a smart_device enabled module that has a motion sensor associated with it which has just changed to a Status.Level>0 (turned on)
            Log("15-" + sensor_mod.Instance.Name + " ; Event time ; " + module.Parameter("Status.Level").Statistics.History[0].Timestamp.ToLocalTime().ToString("yyyyMMdd HH:mm:ss.fffffff"));
            
            if (Night && (sensor_mod.IsOfDeviceType("Light") || sensor_mod.IsOfDeviceType("Dimmer") || sensor_mod.IsOfDeviceType("Switch")))
            {  
              // NIGHT
              // Action: Turn light on
              sensor_mod.On();
              Log("16-" + sensor_mod.Instance.Name + " ; ON");
              
              if (sensor_mod.Parameter(MOT_DELAY_AFTER_OFF).Value=="FALSE")
              {
                Log("16.1-Delay time start ; " + sensor_mod.Parameter(TURNOFF_TIMEOUT).Value);

                sensor_mod.Parameter(TIMER_ON).Value="TRUE";
                // UpdateTime should be automatically updated, but it doesn't seem to be.  This manually forces an update.
                sensor_mod.Parameter(TIMER_ON).UpdateTime=DateTime.Now;
              }
              else
              {
                Log("16.2-Waiting for sensor OFF to set timer ; " + sensor_mod.Parameter(TURNOFF_TIMEOUT).Value);
              }
            }
            else if (sensor_mod.IsOfDeviceType("Siren") && sensor_mod.Parameter("Status.Level").DecimalValue==0)
            {
              Log("16.5-" + sensor_mod.Instance.Name +  " ; Siren Type ; OFF");
              
              if (Night)
              {
                //trip chime
                sensor_mod.On();
                sensor_mod.Parameter(TIMER_ON).Value="TRUE";
                // UpdateTime should be automatically updated, but it doesn't seem to be.  This manually forces an update.
                sensor_mod.Parameter(TIMER_ON).UpdateTime=DateTime.Now;
                
                Log("17-" + sensor_mod.Instance.Name + " ; ON" + " ; Delay time start ; " + sensor_mod.Parameter(TURNOFF_TIMEOUT).Value);
              }
              else
              {
                // Day
                // Action: trip chime if triggered 2x within nuisance limiter timeout
                
                // Double sensor detect
                var event1 = module.Parameter("Status.Level").Statistics.History[0].Timestamp;
                var event2 = module.Parameter("Status.Level").Statistics.History[1].Timestamp;
                var elapsed = new TimeSpan(event1.Ticks - event2.Ticks);
                
                if (elapsed.TotalSeconds < NUISANCE_TIMEOUT)
                {
                  Log("18-Double sensor - Chime ON");
                  
                  sensor_mod.Parameter(TIMER_ON).Value="TRUE";
                  // UpdateTime should be automatically updated, but it doesn't seem to be.  This manually forces an update.
                  sensor_mod.Parameter(TIMER_ON).UpdateTime=DateTime.Now;
                  
                  sensor_mod.On();
                }
              }
            }
          }
          //continue checking remaining items
          return false;
        });
      }
      else
      { // Motion stopped
        Log("18.1-" + module.Instance.Name + " ; " + parameter.Name + " ; " + parameter.Value);
        
        Modules.WithFeature(SMART_LIGHT_ENABLE).Each((sensor_mod)=>
        {
          string fullName = module.Instance.Domain + ":" + module.Instance.Address;
          if ((module.Instance.Name.StartsWith(sensor_mod.Parameter(MOT_SENSOR_NAME).Value) || fullName.StartsWith(sensor_mod.Parameter(MOT_SENSOR_NAME).Value)) 
              && sensor_mod.Parameter(MOT_DELAY_AFTER_OFF).Value=="TRUE")
          {
            if (sensor_mod.Parameter("Status.Level").DecimalValue==1)
            {
              // If the motion sensor turns off and the light is on, assume the motion code turned it on.  
              // If it's off, assume that it was intentional and don't set timer or turn light back on.
              sensor_mod.Parameter(TIMER_ON).Value="TRUE";
              // UpdateTime should be automatically updated, but it doesn't seem to be.  This manually forces an update.
              sensor_mod.Parameter(TIMER_ON).UpdateTime=DateTime.Now;
              
              Log("18.5-Start delay after motion OFF received");
            }
          }
          return false;
        }); 
      }  
    }
  }


  //break; don't check any more items
  return true;
});


// Setup and check timers
while (Program.IsEnabled)
{
  Modules.WithFeature(SMART_LIGHT_ENABLE).Each((timer_mod)=>
  {
    var timeout = timer_mod.Parameter(TURNOFF_TIMEOUT).DecimalValue;
    if (timeout == 0) timeout = 120; // default timeout is 2 minutes
    
    // Check for chime nuisance timeout
    if (timer_mod.IsOfDeviceType("Siren") && timer_mod.Parameter("Status.Level").DecimalValue == 1)
    {
      var event1 = timer_mod.Parameter("Status.Level").Statistics.LastOn.Timestamp;
      var elapsed = new TimeSpan(DateTime.Now.Ticks - event1.Ticks);
      
      if (elapsed.TotalSeconds >= timeout)
      {
        Log("19-" + timer_mod.Instance.Name + " ; chime timeout");
        timer_mod.Off();
      }
    }
    
    // Check if motion based timer is on
    if (timer_mod.Parameter("Status.Level").DecimalValue==1 && timer_mod.Parameter(TIMER_ON).Value=="TRUE")
    {
      var lasteventtime = timer_mod.Parameter(TIMER_ON).UpdateTime;
      //Log("20-" + timer_mod.Instance.Name + " ; lasteventtime = " + lasteventtime.ToLocalTime().ToString("yyyyMMdd HH:mm:ss.fffffff"));
      
      if (timer_mod.Parameter(TIMER_ON).Value=="TRUE")
      {
        var elapsed = new TimeSpan(DateTime.Now.Ticks - lasteventtime.Ticks);
        
        if (elapsed.TotalSeconds > timeout) 
        {
          timer_mod.Off();
          timer_mod.Parameter(TIMER_ON).Value="FALSE";
          // UpdateTime should be automatically updated, but it doesn't seem to be.  This manually forces an update.
          timer_mod.Parameter(TIMER_ON).UpdateTime=DateTime.Now;

          Log("21-" + timer_mod.Instance.Name + " ; timeout = " + timeout.ToString() + " ; elapsed = " + elapsed.TotalSeconds + " ; timeout reached, turning OFF");
        }
      }
    }
    
    return false;
  });

  Pause(TIMER_FIDELITY);
}

Program.GoBackground();

