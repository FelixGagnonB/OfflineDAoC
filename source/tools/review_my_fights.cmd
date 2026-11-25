@echo off
rem Writes the pages of your newest 8v8 evenings and opens the list in your browser.
rem  review_my_fights.cmd            the game you play (D:\OfflineDAoC)
rem  review_my_fights.cmd --scrim    the copy for trying things
python "%~dp0pvp_fights_page.py" --open %*
if errorlevel 1 pause
