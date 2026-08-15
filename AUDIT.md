# COM Interaction Performance Audit

## Summary
The application suffers from a "death by a thousand cuts" performance issue. While the high-level approach (using Queries) is correct, the low-level data extraction process creates an enormous volume of COM round-trips (IDispatch calls), leading to significant latency.

## Identified Bottlenecks

### 1. Chatty Row Iteration (High Severity)
- **Location**: `CatalogReader.Read` (lines 84-110), `PriceLoader.ProcessPriceTable` (lines 95-125).
- **Issue**: Using `selection.Next()` or `table.Get(i)` in a loop.
- **Impact**: Every call to `Next()` or `Get()` is a COM transition.
- **Recommendation**: Always use `query.Execute().Unload()` to move the entire result set into a `ValueTable` in memory, reducing the overhead of navigating the result set.

### 2. Property Access Overhead (High Severity)
- **Location**: `CatalogReader.MapRecord` (lines 343-424), `PriceLoader.ProcessPriceTable` (lines 98-109).
- **Issue**: Accessing fields via `dynamic` or `InvokeMember` inside nested loops (rows $\times$ columns).
- **Impact**: For 10k records and 10 columns, this generates $\approx 100,000$ COM calls just to read raw values.
- **Recommendation**: Minimize property access. If possible, use a more efficient way to extract row data or batch the extraction.

### 3. Heavy Value Normalization (Critical Severity)
- **Location**: `ComValueMapper.Normalize` (lines 104-137).
- **Issue**: For every single COM value, the mapper performs up to 3 COM calls: `String(value)`, `ТипЗнч(value)`, and `String(typeName)`.
- **Impact**: This multiplies the COM traffic by 3x for every column in every row.
- **Recommendation**: 
    - Cache the type of the column once per batch instead of per value.
    - Avoid `ТипЗнч` calls inside the inner loop.
    - Use faster checks for empty/null COM objects.

### 4. Non-Cached GUID Extraction (Medium Severity)
- **Location**: `CatalogReader.GetRefId` (lines 446-490).
- **Issue**: This method is a static helper that does NOT use the `ReferenceResolver` cache.
- **Impact**: Redundant calls to `УникальныйИдентификатор` for the same references.
- **Recommendation**: Replace all calls to `CatalogReader.GetRefId` with `ReferenceResolver.GetRefGuid` to leverage the IUnknown pointer cache.

## Safety & Constraints
- **No Signature Changes**: All public methods (`Read`, `LoadPrices`, etc.) must maintain their current signatures to avoid breaking dependent services.
- **Verification**: Each optimization must be verified against existing data to ensure no regressions in value mapping (especially `null` vs `empty` handling).

---

## Implementation Roadmap
1. [ ] Refactor `CatalogReader` to use `Unload()` and `ReferenceResolver`.
2. [ ] Optimize `ComValueMapper.Normalize` to eliminate redundant COM calls.
3. [ ] Refactor `PriceLoader` to use the same optimized patterns.
4. [ ] Final end-to-end performance validation.
