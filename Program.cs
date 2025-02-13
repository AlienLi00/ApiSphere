//时间本地化格式
using AS.Lib;
using AS.TimedExecutService;
using System.Globalization;

Thread.CurrentThread.CurrentUICulture = new CultureInfo("zh-Hans");
Thread.CurrentThread.CurrentUICulture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
Thread.CurrentThread.CurrentUICulture.DateTimeFormat.ShortTimePattern = "HH:mm:ss.fff";
CultureInfo ci = new CultureInfo("zh-Hans");
ci.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
ci.DateTimeFormat.ShortTimePattern = "HH:mm:ss.fff";
Thread.CurrentThread.CurrentCulture = ci; 

var builder = WebApplication.CreateBuilder(args);
//json日期格式化
builder.Services.AddControllers().AddJsonOptions(json => { json.JsonSerializerOptions.Converters.Add(new DatetimeJsonConverter()); });

// Add services to the container.
//定时任务组件
builder.Services.AddSingleton<IHostedService, TimedTaskCore>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
