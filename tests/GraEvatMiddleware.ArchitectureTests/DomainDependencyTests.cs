using GraEvatMiddleware.Domain.Transactions;

namespace GraEvatMiddleware.ArchitectureTests;

public sealed class DomainDependencyTests
{
    [Fact]
    public void Domain_DoesNotReferenceOtherSolutionProjects()
    {
        var forbiddenReferences = typeof(TransactionStatus)
            .Assembly
            .GetReferencedAssemblies()
            .Where(reference => reference.Name?.StartsWith("GraEvatMiddleware.", StringComparison.Ordinal) == true)
            .Select(reference => reference.Name)
            .ToArray();

        Assert.Empty(forbiddenReferences);
    }
}
