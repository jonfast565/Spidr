
use webdb;

/*
drop table FileTable;
drop table LinkTable;
drop table PageTable;
*/

create table PageTable (
	PageId varchar(40) not null primary key,
	Tag varchar(5000),
    Title varchar(5000) null,
    Content longblob
);

create table FileTable (
    FileId varchar(40) not null primary key,
	PageId varchar(40) not null references PageTable (PageId),
	Tag varchar(5000),
    Path varchar(5000),
    Filename varchar(1000),
    TypeDesc varchar(1000),
    FileContents longblob
);

create table LinkTable (
	LinkId varchar(40) not null primary key,
	PageId varchar(40) not null references PageTable (PageId),
	Tag varchar(5000),
    Path varchar(5000)
);

truncate table FileTable;
truncate table PageTable;
truncate table LinkTable;

