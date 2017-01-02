# Mayflower.NET

Mayflower is a simple, forward-only, database migrator for SQL Server. It is based on the migrator which Stack Overflow uses.

Supports .NET Framework 4.6.1 or .NET Core 1.0.

## Usage

### Creating Migrations

A migration is just a .sql file. Individual commands can be separated with the `GO` keyword, just like when using [SSMS](https://msdn.microsoft.com/en-us/library/mt238290.aspx). For example:

```sql
CREATE TABLE One
(
  Id int not null identity(1,1),
  Name nvarchar(50) not null,
  
  constraint PK_One primary key clustered (Id)
)
GO

INSERT INTO One (Name) VALUES ('Wystan')
GO
```

> Migrations are run in a transaction by default, which allows them to be rolled back if any command fails. You can disable this transaction for a specific migration by beginning the file with `-- no transaction --`.

We recommend prefixing migration file names with a zero-padded number so that the migrations are listed in chronological order. For example, a directory of migrations might look like:

```
0001 - Add Users table.sql
0002 - Add Posts.sql
0003 - Insert default users.sql
0004 - Add auth columns to Users.sql
...
```

### Running Migrations

#### Programmatic

```csharp
using Mayflower;

var options = new Options
{
    ConnectionString = "connection string to SQL Server database",
    Directory = @"c:\path\to\migrations",
    Output = Console.Out,
};

var result = Migrator.RunOutstandingMigrations(options);
// result.Success indicates success or failure
```

If you use integrated auth, then you don't have to provide a connection string. Simply provide the database and server.

```csharp
var options = new Options
{
    Database = "MyDatabase",
    Server = "localhost", // optional (detaults to localhost)
    Directory = @"c:\path\to\migrations",
    Output = Console.Out,
};
```

The [Options](https://github.com/bretcope/Mayflower.NET/blob/master/Mayflower/Options.cs) class has additional properties you can explore, such as `CommandTimeout`, which may be useful.

#### Command Line

>  TODO: Create a repo/nuget for a CLI so people don't have to implement it themselves.

Mayflower itself is just a class library. However, the `Cli` static class makes it really easy to wrap in a command line interface. Here is a complete implementation of a CLI for Mayflower:

```csharp
using System;

namespace MayflowerCLI
{
    static class Program
    {
        static int Main(string[] args)
        {
            Mayflower.Cli.ExeName = "MayflowerCLI";
            return Mayflower.Cli.Execute(args, Console.Out) ? 0 : 1;
        }
    }
}
```

If you run that, it will output:

```
Usage: MayflowerCLI [OPTIONS]+
  Runs all *.sql files in the directory --dir <directory>.
  The databse connection can be specified using a full connection string with
  --conn, or Mayflower can generate an integrated auth connection string using
  the --db and optional --server arguments.

OPTIONS:

  --conn <value>        A SQL Server connection string. For integrated auth, you
                          can use --database and --server instead.
  --db <value>          Generates an integrated auth connection string for the
                          specified database.
  --server <value>      Generates an integrated auth connection string with the
                          specified server (default: localhost).
  --dir <value>         The directory containing your .sql migration files
                          (defaults to current working directory).
  --table <value>       Name of the table used to track migrations (default:
                          Migrations).
  --timeout <value>     Command timeout duration in seconds (default: 30).
  --preview             Run outstanding migrations, but roll them back.
  --global              Run all outstanding migrations in a single transaction,
                          if possible.
  --force               Will rerun modified migrations.
  --count               Print the number of outstanding migrations.
  --version             Print the Mayflower version number.
  --help                Shows this help message.
```

### Reverting Migrations

Many migration systems have a notion of reversing a migration or "downgrading" in some sense. Mayflower has no such concept. If you want to reverse the effects of one migration, then you write a new migration to do so. We don't believe in going backwards.

## License

Mayflower is available under the [MIT License](https://github.com/bretcope/Mayflower.NET/blob/master/LICENSE.MIT).

#### Why not just open source the actual Stack Overflow migrator?

Nick Craver put it pretty well [in his blog post](https://nickcraver.com/blog/2016/05/03/stack-overflow-how-we-do-deployment-2016-edition/#database-migrations):

> The database migrator we use is a very simple repo we could open source, but honestly there are dozens out there and the “same migration against n databases” is fairly specific. The others are probably much better and ours is very specific to *only* our needs. The migrator connects to the Sites database, gets the list of databases to run against, and executes all migrations against every one (running multiple databases in parallel). This is done by looking at the passed-in migrations folder and loading it (once) as well as hashing the contents of every file. Each database has a `Migrations` table that keeps track of what has already been run.

Mayflower uses the same basic technique described in the last two sentences, but doesn't have any of the Stack Overflow-specific functionality. Additionally, it was built from the ground-up as a public migrator, rather than trying to adapt our internal codebase, which means it focuses on usability for third parties.

It's true that there are lots of other database migrators out there, but I personally love the extremely simple way we do migrations, so I thought it was worth having a public implementation. And, selfishly, I wanted to be able to use it for my own projects.
