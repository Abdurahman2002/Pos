# تحسينات نظام طباعة الملصقات

## المشاكل التي تم تحديدها

### 1. **أرقام السعر غير واضحة** 
- **السبب الأساسي:** حجم الخط كان صغيراً جداً (10 نقاط فقط)
- **التأثير:** أرقام السعر غير قابلة للقراءة على الملصقات المطبوعة

### 2. **أرقام الباركود غير واضحة**
- **السبب:** حجم الخط كان 7.5 نقطة فقط (صغير جداً)
- **التأثير:** الأرقام أسفل الباركود غير قابلة للقراءة

### 3. **النص العربي مشوّش**
- **السبب:** استخدام خط Tahoma الذي لا يدعم العربية بشكل محترف
- **التأثير:** النصوص العربية قد تظهر بشكل مشوه

---

## الحلول المطبقة

### ✅ تحسين حجم الخطوط في `TsplLabelBuilder.cs`

```csharp
// قبل:
using var shopBmp  = BuildTextBitmap(shopName,    7.5f, bold: true,  ...);
using var nameBmp  = BuildTextBitmap(productName, 10f,  bold: true,  ...);
using var codeBmp  = BuildTextBitmap(cleanCode,   7.5f, bold: false, ...);
using var priceBmp = BuildTextBitmap(priceLine,   10f,  bold: true,  ...);

// بعد:
using var shopBmp  = BuildTextBitmap(shopName,    8f,   bold: true,  ...);
using var nameBmp  = BuildTextBitmap(productName, 11f,  bold: true,  ...);
using var codeBmp  = BuildTextBitmap(cleanCode,   10f,  bold: true,  ...);
using var priceBmp = BuildTextBitmap(priceLine,   12f,  bold: true,  ...);
```

**التحسينات:**
- زيادة حجم السعر من 10f → **12f** (20% أكبر)
- زيادة حجم رقم الباركود من 7.5f → **10f** (33% أكبر)
- زيادة حجم اسم المنتج من 10f → **11f**
- تحسين حجم اسم المتجر من 7.5f → **8f**

### ✅ تحسين دعم العربية - تغيير الخط الافتراضي

```csharp
// قبل:
private const string DefaultFontName = "Tahoma";

// بعد:
private const string DefaultFontName = "Arial";
```

**الفوائد:**
- Arial يوفر دعماً أفضل للعربية من Tahoma
- تحسين جودة عرض النصوص العربية

### ✅ تحسين أولويات البحث عن الخطوط

```csharp
// قبل:
private static DrawingFontFamily ResolveFontFamily()
    => TryResolveFont(DefaultFontName) 
       ?? TryResolveFont("Segoe UI") 
       ?? DrawingFontFamily.GenericSansSerif;

// بعد:
private static DrawingFontFamily ResolveFontFamily()
    => TryResolveFont("Arial") 
       ?? TryResolveFont("Segoe UI") 
       ?? TryResolveFont("Tahoma") 
       ?? DrawingFontFamily.GenericSansSerif;
```

### ✅ تحسين أنماط CSS في `PrintLabel.cshtml`

#### تحسين أرقام الباركود:
```css
/* قبل */
.item-label-code {
    font-size: 7px;
    letter-spacing: 1.5px;
}

/* بعد */
.item-label-code {
    font-size: 9px;
    letter-spacing: 2px;
}
```

#### تحسين السعر:
```css
/* قبل */
.item-label-price {
    font-size: 10px;
    line-height: 1.1;
}

/* بعد */
.item-label-price {
    font-size: 12px;
    line-height: 1.2;
    letter-spacing: 0.5px;
    color: #000;
}
```

#### تحسين ترتيب الخطوط:
```css
/* قبل */
font-family: 'Cairo', 'Tahoma', 'Arial', sans-serif;

/* بعد */
font-family: 'Cairo', 'Arial', 'Tahoma', sans-serif;
```

---

## النتائج المتوقعة

| العنصر | قبل | بعد | التحسن |
|-------|-----|-----|--------|
| حجم السعر | 10px | 12px | 20% أكبر |
| حجم رقم الباركود | 7.5px | 9px | 20% أكبر |
| حجم اسم المنتج | 10px | 11px | 10% أكبر |
| وضوح العربية | Tahoma | Arial | أفضل بكثير |
| تباعد الأرقام | 1.5px | 2px | أوضح |

---

## ملاحظات تقنية

1. **الملصقات المادية:** عند الطباعة على طابعة TSPL حقيقية، ستظهر التحسينات بشكل أوضح
2. **كثافة الطباعة:** إذا كانت الأرقام لا تزال غير واضحة، جرّب زيادة كثافة الطباعة (Density) من 10 → 12-14
3. **الخطوط:** تأكد من تثبيت خط Arial على النظام (عادة ما يكون مثبتاً مسبقاً)

---

## خطوات الاختبار

1. **ادخل إلى صفحة Print Label**
2. **لاحظ أن الأرقام والنصوص أكثر وضوحاً الآن**
3. **اختبر الطباعة على طابعة TSPL الفعلية**
4. **قارن مع الملصقات السابقة**

---

## إذا استمرت المشاكل

إذا كانت الأرقام لا تزال غير واضحة بعد هذه التحسينات:

1. **زيادة الكثافة:** عدّل قيمة `LabelDensity` في إعدادات الطباعة من 10 → 12-15
2. **التحقق من الطابعة:** تأكد من أن رأس الطباعة نظيف وسليم
3. **الاختبار مع محاكاة:** استخدم برنامج محاكي TSPL للتحقق من الإخراج قبل الطباعة الفعلية
4. **جودة الملصقات:** استخدم ملصقات عالية الجودة لضمان طباعة واضحة

