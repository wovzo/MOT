import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

class LiveClassroomScreen extends StatefulWidget {
  final String conferenceID;

  const LiveClassroomScreen({
    super.key,
    required this.conferenceID,
  });

  @override
  State<LiveClassroomScreen> createState() => _LiveClassroomScreenState();
}

class _LiveClassroomScreenState extends State<LiveClassroomScreen> {
  @override
  void initState() {
    super.initState();
    _launchClassroom();
  }

  Future<void> _launchClassroom() async {
    // Generate a secure Jitsi Meet link
    final url = Uri.parse('https://meet.jit.si/MOTClassroom_${widget.conferenceID}');
    
    if (await canLaunchUrl(url)) {
      await launchUrl(url, mode: LaunchMode.externalApplication);
    } else {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not open classroom link!')),
        );
      }
    }
    
    // Once launched, we can pop back to the dashboard since the call is in a new tab/app.
    if (mounted) {
      Navigator.pop(context);
    }
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: Color(0xFF121026),
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            CircularProgressIndicator(color: Color(0xFF6C5CE7)),
            SizedBox(height: 20),
            Text(
              'Opening Live Classroom...',
              style: TextStyle(color: Colors.white, fontSize: 18),
            )
          ],
        ),
      ),
    );
  }
}
