DROP TABLE IF EXISTS MangaPages;
DROP TABLE IF EXISTS Chapters;
DROP TABLE IF EXISTS Mangas;

CREATE TABLE Mangas (
	Id INTEGER PRIMARY KEY AUTOINCREMENT,
	Title TEXT,
	Address TEXT
);


CREATE TABLE Chapters (
	ChapterId INTEGER PRIMARY KEY AUTOINCREMENT,
	Title TEXT,
	Address TEXT,
	Manga_Id INT, 
	FOREIGN KEY(Manga_Id) REFERENCES Mangas(Id)
);

CREATE TABLE MangaPages (
	MangaPageId INTEGER PRIMARY KEY AUTOINCREMENT,
	Title TEXT,
	Address TEXT,
	Progress INT,
	BytesTotal INT,
	BytesDownloaded INT,
	DestinationPath TEXT,
	Chapter_Id INT, 
	FOREIGN KEY(Chapter_Id) REFERENCES Chapters(Id)
);
