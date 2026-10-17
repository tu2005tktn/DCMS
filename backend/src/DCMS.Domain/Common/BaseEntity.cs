using System;

namespace DCMS.Domain.Common;

public abstract class BaseEntity<TId>
{
    public TId Id { get; init; } = default!;

    protected BaseEntity() { }
}
