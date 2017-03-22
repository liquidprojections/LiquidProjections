namespace LiquidProjections
{
    /// <summary>
    /// A delegate that can be implemented to retry projecting a transaction when it fails.
    /// </summary>
    /// <returns>Returns true if the projector should retry to project the transaction, false if it shoud fail with the specified exception.</returns>
    /// <param name="exception">The exception that occured that caused this batch to fail.</param>
    /// <param name="attempts">The number of attempts that were made to project this transaction.</param>
    public delegate bool ShouldRetry(ProjectionException exception, int attempts);
}