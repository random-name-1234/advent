using System;

namespace advent;

public interface IDeferredActivationScene
{
    bool IsReadyToActivate { get; }
    bool ShouldSkipActivation { get; }
    void Prepare();
    void AdvancePreparation(TimeSpan timeSpan);
}
