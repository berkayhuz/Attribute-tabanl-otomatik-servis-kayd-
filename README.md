## 🔍 Genel

.NET uygulamaları için **Attribute tabanlı otomatik servis kaydı** sağlar. 
Reflection kullanarak sınıfları tarar ve yapılandırılmış kurallar dahilinde uygun servisleri otomatik olarak `IServiceCollection` içine ekler. 
Mikroservis mimarisi için uygundur ve genişletilebilir, ölçeklenebilir bir yapı sunar.

## 🛠️ Başlarken

### Sınıfları İşaretle

```csharp
using ServiceDefaults.DependencyInjection.Attributes;

[Register(ServiceLifetime.Scoped, RegisterSelf = true)]
public class MyService : IMyService
{
    ...
}
```

Yalnızca belirli servislerde kullanılmasını istiyorsan:

```csharp
[Register(ServiceLifetime.Singleton)]
[TargetService("NotificationService")]
public class SmtpNotifier : IEmailSender
{
    ...
}
```

---

### Ayarları Yapılandır (Opsiyonel)

```csharp
services.Configure<RegistrationOptions>(opts =>
{
    opts.SpecialInterfaceThreshold = 2;
    opts.ExcludedAssemblyPrefixes = new[] { "System.", "Microsoft." };
    opts.CacheSizeLimit = 512;
    opts.CacheEntryExpirationSeconds = 1800;
});
```

---

### 4. Program.cs İçinde Kaydı Başlat

```csharp
services.RegisterAttributedServices("NotificationService");
```

Bu satır, `ILoggerFactory` ve `IOptions<RegistrationOptions>` gibi bağımlılıkları kendi içerisinde çözer.

---

## ⚙️ Öznitelikler

### `[Register]`

Bir sınıfı servis olarak kayıt etmek için.

| Özellik         | Açıklama                            |
|------------------|--------------------------------------|
| `Lifetime`       | `Singleton`, `Scoped`, `Transient`  |
| `RegisterSelf`   | Sınıfın kendisini de kayıt et       |

### `[TargetService]`

Belirli mikroservislere özel kayıt sağlar:

```csharp
[TargetService("IdentityService", "AuthService")]
```

---

## 💡 Örnekler

### 1. Standart Sınıf

```csharp
[Register(ServiceLifetime.Scoped)]
public class OrderService : IOrderService
```

---

```csharp
[Register(ServiceLifetime.Singleton, RegisterSelf = true)]
public class JsonOptionsProvider
```
