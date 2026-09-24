using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Inventor;
using InventorModel.Core.Diagnostics;
using InventorModel.Core.Dsl;

namespace InventorModel.Inventor;

public sealed class ParameterUpdater
{
    private readonly Application _application;

    public ParameterUpdater(Application application)
    {
        _application = application ??
            throw new ArgumentNullException(nameof(application));
    }

    public void Apply(
        PartDocument document,
        ModelScript targetModel,
        IEnumerable<ParameterValueChange> changes)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (targetModel == null)
            throw new ArgumentNullException(nameof(targetModel));
        if (changes == null)
            throw new ArgumentNullException(nameof(changes));

        HashSet<string> changedNames =
            changes
                .Select(x => x.Name)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        if (changedNames.Count == 0)
            return;

        PartComponentDefinition component =
            document.ComponentDefinition;

        Dictionary<string, UserParameter> existing =
            component.Parameters.UserParameters
                .Cast<UserParameter>()
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        foreach (string name in changedNames)
        {
            if (!existing.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    "Incremental parameter update cannot find Inventor parameter '" +
                    name + "'.");
            }
        }

        Transaction transaction =
            _application.TransactionManager.StartTransaction(
                (_Document)(object)document,
                "InventorModel parameter update");

        try
        {
            foreach (ParameterStatement parameter in
                     targetModel.Statements
                         .OfType<ParameterStatement>())
            {
                if (!changedNames.Contains(
                        parameter.Name))
                {
                    continue;
                }

                existing[parameter.Name].Expression =
                    InventorExpression(
                        parameter.Expression);
            }

            document.Update2(true);
            transaction.End();
        }
        catch (Exception ex)
        {
            try
            {
                transaction.Abort();
            }
            catch (Exception abortException)
            {
                RuntimeLog.Error(
                    "Inventor.Transaction",
                    "Parameter update rollback failed.",
                    abortException);
            }

            RuntimeLog.Error(
                "Inventor.Parameters",
                "Incremental parameter update failed.",
                ex);
            throw;
        }
    }

    private static string InventorExpression(
        string expression)
    {
        string text =
            expression.Trim();

        if (text.IndexOf(
                "mm",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            text.IndexOf(
                "deg",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return text;
        }

        if (double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out _))
        {
            return text + " mm";
        }

        return text;
    }
}
