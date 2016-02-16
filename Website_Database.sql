
use webdb;

/*
drop table FileTable;
drop table LinkTable;
drop table PageTable;
*/

create table FileTable (
	PageId varchar(40),
	Tag varchar(1000),
    Path varchar(1000),
    Filename varchar(1000),
    TypeDesc varchar(1000),
    FileContents longblob
);

create table LinkTable (
	PageId varchar(40),
	Tag varchar(1000),
    Path varchar(1000)
);

create table PageTable (
	PageId varchar(40),
	Tag varchar(1000),
    Title varchar(1000),
    Content longblob
);

truncate table FileTable;
truncate table PageTable;
truncate table LinkTable;

