using System;
using Inventor;
using InventorModel.Core.Dsl;
using InventorModel.Inventor;
using Xunit;

namespace InventorModel.Inventor.Tests;

[CollectionDefinition(
    "Inventor integration",
    DisableParallelization = true)]
public sealed class InventorIntegrationCollection
{
}

[Collection("Inventor integration")]
public sealed class InventorLifecycleTests
{
    [Fact]
    public void FailedBuildRetriesReuseOneWorkingPart()
    {
        InventorSession session =
            InventorSession.Connect();
        Application application =
            session.Application;
        application.Visible = true;

        var documents =
            new WorkingDocumentManager(
                application);

        int before =
            application.Documents.Count;
        PartDocument document =
            documents.AcquireForBuild();

        try
        {
            int afterCreate =
                application.Documents.Count;
            Assert.Equal(
                before + 1,
                afterCreate);

            const string failingSource =
                "part Retry\n" +
                "fillet bad edges 1 radius 2";

            var executor =
                new ScriptExecutor(
                    application);

            for (int i = 0; i < 3; i++)
            {
                PartDocument acquired =
                    documents.AcquireForBuild();

                Assert.Same(
                    document,
                    acquired);

                Assert.ThrowsAny<Exception>(
                    () =>
                        executor.Execute(
                            failingSource,
                            acquired,
                            replaceExisting: true));

                Assert.Equal(
                    afterCreate,
                    application.Documents.Count);
            }
        }
        finally
        {
            document.Close(true);
        }
    }

    [Fact]
    public void ParameterUpdateKeepsSameWorkingPart()
    {
        InventorSession session =
            InventorSession.Connect();
        Application application =
            session.Application;
        application.Visible = true;

        var documents =
            new WorkingDocumentManager(
                application);
        PartDocument document =
            documents.AcquireForBuild();

        try
        {
            const string beforeSource =
                "part Plate\n" +
                "param width = 100\n" +
                "sketch base on XY\n" +
                "rect 0 0 width 20\n" +
                "end\n" +
                "extrude body from base depth 10 join";

            const string afterSource =
                "part Plate\n" +
                "param width = 120\n" +
                "sketch base on XY\n" +
                "rect 0 0 width 20\n" +
                "end\n" +
                "extrude body from base depth 10 join";

            new ScriptExecutor(application)
                .Execute(
                    beforeSource,
                    document,
                    replaceExisting: true);

            var parser =
                new DslParser();
            ModelScript before =
                parser.Parse(beforeSource);
            ModelScript after =
                parser.Parse(afterSource);
            ModelDiffResult diff =
                new ModelDiffer()
                    .Compare(
                        before,
                        after);

            Assert.True(
                diff.IsParameterOnly);

            new ParameterUpdater(application)
                .Apply(
                    document,
                    after,
                    diff.ParameterChanges);

            Assert.Same(
                document,
                documents.AcquireForBuild());

            UserParameter? width = null;
            foreach (UserParameter parameter in
                     document.ComponentDefinition
                         .Parameters
                         .UserParameters)
            {
                if (parameter.Name.Equals(
                        "width",
                        StringComparison.OrdinalIgnoreCase))
                {
                    width = parameter;
                    break;
                }
            }

            Assert.NotNull(width);
            Assert.Contains(
                "120",
                width!.Expression);
        }
        finally
        {
            document.Close(true);
        }
    }
}
