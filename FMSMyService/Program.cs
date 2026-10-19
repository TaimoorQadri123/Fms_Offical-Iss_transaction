using FMSISSTransactionService;
using FMSISSTransactionService.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddTransient<CommonMethods>();
builder.Services.AddTransient<ServiceRun>();
builder.Services.AddHostedService<Worker>();

builder.Services.AddWindowsService();

var host = builder.Build();
host.Run();




//using FMSMyService;

//var builder = Host.CreateApplicationBuilder(args);
//builder.Services.AddHostedService<Worker>();

//var host = builder.Build();
//host.Run();
