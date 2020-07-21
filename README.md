# openhours-cs
Yes, openhours, but in C#. AGAIN

## dev commands

- `dotnet new classlib -f netcoreapp3.1 -n Chneau.Time -o . --force` to init the csproj file and Class1.cs file
- `dotnet new gitignore --force` to init the gitignore file

Nope:
- `dotnet new xunit -o Tests` to init a test package?
- `dotnet test Tests` to run tests

Now:
- https://stackoverflow.com/a/56646051 
- using the files generated from `dotnet new xunit -o Tests` modify csproj file to contains tests when not realease
- ignoring all `*.Tests.cs` file when release
- it shoulds look like this:
```xml
  <ItemGroup Condition="'$(Configuration)' == 'Release'">
    <Compile Remove="**\*.Tests.cs" />
  </ItemGroup>
  <ItemGroup Condition="'$(Configuration)' != 'Release'">
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="16.5.0" />
    <PackageReference Include="xunit" Version="2.4.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.0" />
    <PackageReference Include="coverlet.collector" Version="1.2.0" />
  </ItemGroup>
```
- `dotnet test` to run tests
- `dotnet watch test` to run tests when file is modified - the best.
- Out of context: `dotnet build --configuration Release` to build realease
- Out of context: `dotnet build --runtime ubuntu.18.04-x64` to build for ubuntu

