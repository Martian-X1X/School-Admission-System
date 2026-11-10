using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Application.Services;
using SchoolAdmission.Application.Settings;

namespace SchoolAdmission.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AdmissionSettings>()
            .Bind(configuration.GetSection(AdmissionSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<ClassService>();
        services.AddScoped<HoldService>();

        return services;
    }
}
