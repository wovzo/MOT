import 'package:flutter/material.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF121026),
      appBar: AppBar(
        title: const Text('Settings', style: TextStyle(color: Colors.white)),
        backgroundColor: Colors.transparent,
        elevation: 0,
        iconTheme: const IconThemeData(color: Colors.white),
      ),
      body: ListView(
        children: [
          ListTile(
            leading: const Icon(Icons.person, color: Colors.white70),
            title: const Text('Account', style: TextStyle(color: Colors.white)),
            trailing: const Icon(Icons.chevron_right, color: Colors.white54),
            onTap: () {
              ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Account settings coming soon!')));
            },
          ),
          ListTile(
            leading: const Icon(Icons.notifications, color: Colors.white70),
            title: const Text('Notifications', style: TextStyle(color: Colors.white)),
            trailing: const Icon(Icons.chevron_right, color: Colors.white54),
            onTap: () {
              ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Notification settings coming soon!')));
            },
          ),
          ListTile(
            leading: const Icon(Icons.color_lens, color: Colors.white70),
            title: const Text('Theme', style: TextStyle(color: Colors.white)),
            trailing: const Icon(Icons.chevron_right, color: Colors.white54),
            onTap: () {
              ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Theme settings coming soon!')));
            },
          ),
        ],
      ),
    );
  }
}
