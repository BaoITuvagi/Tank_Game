PRAGMA foreign_keys=ON;
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;
CREATE TABLE IF NOT EXISTS accounts(id INTEGER PRIMARY KEY,username TEXT UNIQUE NOT NULL,salt BLOB NOT NULL,hash BLOB NOT NULL,iterations INTEGER NOT NULL,created INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS matches(id INTEGER PRIMARY KEY,map_id INTEGER,map_hash TEXT,mode TEXT,started INTEGER,ended INTEGER,reason INTEGER,seed INTEGER,duration_ticks INTEGER,room_mode TEXT,content_version TEXT);
CREATE TABLE IF NOT EXISTS match_players(match_id INTEGER REFERENCES matches(id),account_id INTEGER REFERENCES accounts(id),nickname TEXT,slot INTEGER,kills INTEGER,deaths INTEGER,rank INTEGER,disconnected INTEGER,nation_id INTEGER,PRIMARY KEY(match_id,account_id));
CREATE TABLE IF NOT EXISTS player_profiles(account_id INTEGER PRIMARY KEY REFERENCES accounts(id),display_name TEXT NOT NULL);
