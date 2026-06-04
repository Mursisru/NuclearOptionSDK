using NuclearOptionSDK.Studio.Smoke;

var code = await StudioSmokeRunner.RunAsync(
    Path.Combine(AppContext.BaseDirectory, "smoke-output"));
Environment.Exit(code);
return code;