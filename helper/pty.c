//
// pty.c: helper to open a PTY as a shared library
//
#include <stdio.h>
#include <stdlib.h>
#include <unistd.h>
#include <util.h>

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
