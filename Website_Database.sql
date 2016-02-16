
use webdb;

/*
drop table FileTable;
drop table LinkTable;
drop table PageTable;
*/

create table FileTable (
	PageId varchar(40),
	Tag varchar(5000),
    Path varchar(5000),
    Filename varchar(1000),
    TypeDesc varchar(1000),
    FileContents longblob
);

create table LinkTable (
	PageId varchar(40),
	Tag varchar(5000),
    Path varchar(5000)
);

create table PageTable (
	PageId varchar(40),
	Tag varchar(5000),
    Title varchar(5000),
    Content longblob
);

truncate table FileTable;
truncate table PageTable;
truncate table LinkTable;

