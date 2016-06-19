create table Three
(
	Id int not null identity(1,1),
	Name nvarchar(50) not null,

	constraint PK_Three primary key clustered (Id),
)
GO