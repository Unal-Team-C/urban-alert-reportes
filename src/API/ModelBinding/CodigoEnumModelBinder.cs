using Application.Comun;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace API.ModelBinding;

// Los parámetros de ruta y consulta no pasan por System.Text.Json; este binder
// aplica la misma convención UPPER_SNAKE_CASE que el cuerpo JSON (?estado=EN_INTERVENCION).
public class CodigoEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        Type tipo = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        if (!tipo.IsEnum || context.BindingInfo.BindingSource == BindingSource.Body)
            return null;

        return (IModelBinder)Activator.CreateInstance(typeof(CodigoEnumModelBinder<>).MakeGenericType(tipo))!;
    }
}

public class CodigoEnumModelBinder<TEnum> : IModelBinder where TEnum : struct, Enum
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ValueProviderResult valor = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valor == ValueProviderResult.None || string.IsNullOrEmpty(valor.FirstValue))
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valor);

        if (CodigoEnum.TryParse(valor.FirstValue, out TEnum resultado))
        {
            bindingContext.Result = ModelBindingResult.Success(resultado);
        }
        else
        {
            string permitidos = string.Join(", ", Enum.GetValues<TEnum>().Select(CodigoEnum.ACodigo));
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName, $"'{valor.FirstValue}' no es válido. Valores permitidos: {permitidos}.");
        }

        return Task.CompletedTask;
    }
}
