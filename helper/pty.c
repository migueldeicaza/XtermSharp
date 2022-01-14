//
// pty.c: helper to open a PTY as a shared library
//
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <util.h>
#include <termios.h>
#include <sys/ioctl.h>

pid_t
fork_and_exec (const char *name, char *const args[], char *const env[], int *master, struct winsize *size)
{ 
	pid_t pid = forkpty (master, NULL, NULL, size);
	if (pid < 0)
		return pid;

	if (pid == 0) {
		execve (name, args, env);
		_exit (1);
	}
	return pid;
}

int set_window_size (int fd, struct winsize *size)
{ 
	return ioctl(fd, TIOCSWINSZ, size);
}
