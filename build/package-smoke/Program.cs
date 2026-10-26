using AutoMapper;
using DevExtreme.AspNet.Data;

int factor = 1;
var mapper = new MapperConfiguration(cfg => cfg.CreateMap<Row, Dto>()
    .ForMember(dto => dto.Scaled, options => options.MapFrom(row => row.Value * factor))).CreateMapper();
var result = await DataSourceLoader.LoadAsync<Row, Dto>(
    new[] { new Row { Id = 1, Value = 1 }, new Row { Id = 2, Value = 2 } }.AsQueryable(),
    new DataSourceLoadOptionsBase {
        Filter = new object[] { "Scaled", ">", 15 },
        Select = new[] { "Id", "Scaled" }, RemoteSelect = false,
        RequireTotalCount = true, AllowAsyncOverSync = true
    }, mapper, new { factor = 10 });
var selected = result.data.Cast<IDictionary<string, object>>().Single();
if(result.totalCount != 1 || (int)selected["Id"] != 2 || (int)selected["Scaled"] != 20)
    throw new InvalidOperationException("The packaged typed loader returned incorrect data.");

Console.WriteLine($"Package smoke test passed: {typeof(DataSourceLoader).Assembly.GetName()}");

public class Row {
    public int Id { get; set; }
    public int Value { get; set; }
}

public class Dto {
    public int Id { get; set; }
    public int Scaled { get; set; }
}
