create table One
(
	Id int not null identity(1,1),

	constraint PK_One primary key clustered (Id),
)
GO

create table Two
(
	Id int not null identity(1,1),

	constraint PK_Two primary key clustered (Id),
)
GO